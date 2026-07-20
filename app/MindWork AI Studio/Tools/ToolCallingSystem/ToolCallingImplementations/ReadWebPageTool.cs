using System.Text.Json;
using System.Text.Json.Nodes;
using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

public sealed class ReadWebPageTool(WebPageRetrievalService webPageRetrievalService, ILogger<ReadWebPageTool> logger) : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool));

    private const int DEFAULT_TIMEOUT_SECONDS = 30;
    private const int DEFAULT_MAX_CONTENT_CHARACTERS = 30000;
    private const int MAX_TIMEOUT_SECONDS = 60;
    private const int MAX_CONTENT_CHARACTERS = 50000;
    private const int MAX_TRACE_LENGTH = 12000;
    private const string ALLOWED_PRIVATE_HOSTS_SETTING = "allowedPrivateHosts";

    public string ImplementationKey => ToolSelectionRules.READ_WEB_PAGE_TOOL_ID;

    public string Icon => Icons.Material.Filled.Article;

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public static string GetDisplayName() => TB("Read Web Page");

    public string GetDescription() => TB("Load a web page and extract its readable content, links, and page details.");

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "timeoutSeconds" => TB("Timeout Seconds"),
        "maxContentCharacters" => TB("Maximum Content Characters"),
        ALLOWED_PRIVATE_HOSTS_SETTING => TB("Allowed Private Hosts"),
        _ => TB(fieldDefinition.Title),
    };

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "timeoutSeconds" => TB("(Optional) HTTP timeout for loading a web page in seconds."),
        "maxContentCharacters" => TB("(Optional) Global truncation limit for extracted characters returned to the model."),
        ALLOWED_PRIVATE_HOSTS_SETTING => TB("(Optional) Host allowlist for private or VPN web pages. For security reasons, private or VPN web pages aren't allowed to be read by default. Separate host patterns with commas, such as example.de, *.example.de. Allowed private hosts require a high-confidence provider. For allowed internal hosts, AI Studio also tries the operating system's default sign-in automatically when the server responds with integrated authentication."),
        _ => TB(fieldDefinition.Description),
    };

    public string? GetSettingsFieldDefaultValue(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "timeoutSeconds" => DEFAULT_TIMEOUT_SECONDS.ToString(),
        "maxContentCharacters" => DEFAULT_MAX_CONTENT_CHARACTERS.ToString(),
        _ => null,
    };

    public Task<ToolConfigurationState?> ValidateConfigurationAsync(
        ToolDefinition definition,
        IReadOnlyDictionary<string, string> settingsValues,
        CancellationToken token = default)
    {
        if (!TryReadOptionalPositiveInt(settingsValues, "timeoutSeconds", out _, out var timeoutError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = timeoutError,
            });
        }

        if (!TryReadOptionalPositiveInt(settingsValues, "maxContentCharacters", out _, out var contentError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = contentError,
            });
        }

        if (!TryReadAllowedPrivateHostPatterns(settingsValues.GetValueOrDefault(ALLOWED_PRIVATE_HOSTS_SETTING), out _, out var allowlistError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = allowlistError,
            });
        }

        return Task.FromResult<ToolConfigurationState?>(null);
    }

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default)
    {
        var urlText = ReadRequiredString(arguments, "url");
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var url) || url is not { Scheme: "http" or "https" })
            throw new ArgumentException("Argument 'url' must be a valid HTTP or HTTPS URL.");

        var timeoutSeconds = Math.Min(ReadOptionalPositiveIntSetting(context.SettingsValues, "timeoutSeconds") ?? DEFAULT_TIMEOUT_SECONDS, MAX_TIMEOUT_SECONDS);
        var maxContentCharacters = Math.Min(ReadOptionalPositiveIntSetting(context.SettingsValues, "maxContentCharacters") ?? DEFAULT_MAX_CONTENT_CHARACTERS, MAX_CONTENT_CHARACTERS);
        if (!TryReadAllowedPrivateHostPatterns(context.SettingsValues.GetValueOrDefault(ALLOWED_PRIVATE_HOSTS_SETTING), out var allowedPrivateHosts, out var allowlistError))
            throw new InvalidOperationException(allowlistError);
        RetrievedWebPage retrievedPage;
        try
        {
            retrievedPage = await webPageRetrievalService.RetrieveAsync(
                url,
                new WebPageRetrievalOptions
                {
                    TimeoutSeconds = timeoutSeconds,
                    ProviderConfidence = context.ProviderConfidence,
                    UseOsSso = true,
                    IsPrivateHostAllowed = host => IsAllowedPrivateHost(host, allowedPrivateHosts),
                    OnPrivateHostProviderBlockAsync = this.ReportPrivateHostProviderBlockAsync,
                },
                token);
        }
        catch (WebPageAccessBlockedException exception)
        {
            throw new ToolExecutionBlockedException(exception.Message);
        }
        var page = retrievedPage.Page;
        var extractedPage = retrievedPage.ExtractedPage;
        var markdown = extractedPage.Markdown;
        var originalContentCharacters = markdown.Length;
        List<string> warnings = [];

        if (string.IsNullOrWhiteSpace(markdown))
            warnings.Add("No readable static page content was extracted. The page may require JavaScript, authentication, or browser cookies.");
        else if (markdown.Length < 500)
            warnings.Add("Only a small amount of readable page content was extracted; the result may be incomplete.");

        var contentTruncated = false;
        if (markdown.Length > maxContentCharacters)
        {
            markdown = MarkdownTruncator.Truncate(markdown, maxContentCharacters);
            contentTruncated = true;
            warnings.Add($"The extracted page content was truncated from {originalContentCharacters} to {markdown.Length} characters.");
        }

        return new ToolExecutionResult
        {
            JsonContent = BuildModelContent(page, extractedPage, markdown, originalContentCharacters, contentTruncated, warnings)
        };
    }

    private static JsonNode? BuildModelContent(
        HTMLParserWebPage page,
        ExtractedWebPage extractedPage,
        string websiteContentAsMarkdown,
        int originalContentCharacters,
        bool contentTruncated,
        IReadOnlyList<string> warnings)
    {
        var metadata = new JsonObject
        {
        };

        var status = string.IsNullOrWhiteSpace(websiteContentAsMarkdown)
            ? "empty response"
            : contentTruncated || originalContentCharacters < 500
                ? "partial"
                : "complete";
        var warningArray = new JsonArray();
        foreach (var warning in warnings)
            warningArray.Add(warning);
        
        AddIfNotEmpty(metadata, "language", extractedPage.Language);
        AddIfNotEmpty(metadata, "published_time", extractedPage.PublishedTime);
        AddIfNotEmpty(metadata, "modified_time", extractedPage.ModifiedTime);
        AddIfNotEmpty(metadata, "media_type", page.ContentType);
        metadata["warnings"] = warningArray;
        if (contentTruncated)
        {
            metadata["original_content_characters"] = originalContentCharacters;
            metadata["returned_content_characters"] = websiteContentAsMarkdown.Length;
        }
        
        var content = new JsonObject
        {
            ["text_content"] = websiteContentAsMarkdown,
        };
        
        AddIfNotEmpty(content, "title", extractedPage.Title);
        AddIfNotEmpty(content, "description", extractedPage.Description);
        AddStringArrayIfNotEmpty(content, "authors", extractedPage.Authors);
        

        var result = new JsonObject
        {
            ["url"] = page.RequestedUrl.ToString(),
            ["status"] = status,
            ["retrieved_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["content"] = content,
            ["metadata"] = metadata,
        };
        
        return result;
    }

    private static void AddIfNotEmpty(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target[propertyName] = value;
    }

    private static void AddStringArrayIfNotEmpty(JsonObject target, string propertyName, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return;

        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);
        target[propertyName] = array;
    }

    public string FormatTraceResult(string rawResult)
    {
        if (rawResult.Length <= MAX_TRACE_LENGTH)
            return rawResult;

        return $"{rawResult[..MAX_TRACE_LENGTH]}...";
    }

    private async Task ReportPrivateHostProviderBlockAsync(Uri url, ConfidenceLevel providerConfidence)
    {
        logger.LogWarning(
            "Blocked read_web_page access to allowed private host '{Host}' because provider confidence '{ProviderConfidence}' is below HIGH.",
            url.Host,
            providerConfidence);

        await MessageBus.INSTANCE.SendError(new DataErrorMessage(
            Icons.Material.Filled.Security,
            TB("The web page was not loaded because private or VPN web pages require a High-confidence provider.")));
    }

    private static bool IsAllowedPrivateHost(string host, IReadOnlyList<AllowedPrivateHostPattern> allowedPrivateHosts)
    {
        var normalizedHost = NormalizeHost(host);
        return allowedPrivateHosts.Any(pattern => pattern.IsMatch(normalizedHost));
    }

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();

    private static bool TryReadAllowedPrivateHostPatterns(
        string? rawValue,
        out List<AllowedPrivateHostPattern> patterns,
        out string error)
    {
        patterns = [];
        error = string.Empty;

        foreach (var rawPattern in SplitAllowedPrivateHostPatterns(rawValue))
        {
            var pattern = NormalizeHost(rawPattern);
            if (pattern.Contains("://", StringComparison.Ordinal) || pattern.Contains('/'))
            {
                error = TB("Allowed private hosts must be host names only, without scheme or path.");
                return false;
            }

            var isWildcard = pattern.StartsWith("*.", StringComparison.Ordinal);
            var host = isWildcard ? pattern[2..] : pattern;
            if (string.IsNullOrWhiteSpace(host) || Uri.CheckHostName(host) is UriHostNameType.Unknown)
            {
                error = string.Format(TB("Allowed private host '{0}' is not valid."), rawPattern);
                return false;
            }

            patterns.Add(new AllowedPrivateHostPattern(host, isWildcard));
        }

        patterns = patterns
            .Distinct()
            .ToList();
        return true;
    }

    private static IEnumerable<string> SplitAllowedPrivateHostPatterns(string? rawValue) => rawValue?
        .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x)) ?? [];

    private static string ReadRequiredString(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var value) || value.ValueKind is not JsonValueKind.String)
            throw new ArgumentException($"Missing required argument '{propertyName}'.");

        var text = value.GetString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException($"Missing required argument '{propertyName}'.");

        return text;
    }

    private static int? ReadOptionalPositiveIntSetting(IReadOnlyDictionary<string, string> settingsValues, string key)
    {
        if (!settingsValues.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value, out var parsedValue) && parsedValue > 0 ? parsedValue : null;
    }

    private static bool TryReadOptionalPositiveInt(
        IReadOnlyDictionary<string, string> settingsValues,
        string key,
        out int? value,
        out string error)
    {
        value = null;
        error = string.Empty;

        if (!settingsValues.TryGetValue(key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            return true;

        if (int.TryParse(rawValue, out var parsedValue) && parsedValue > 0)
        {
            value = parsedValue;
            return true;
        }

        error = I18N.I.T($"The setting '{key}' must be a positive integer.", typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool));
        return false;
    }

    private readonly record struct AllowedPrivateHostPattern(string Host, bool IsWildcard)
    {
        public bool IsMatch(string normalizedHost) =>
            this.IsWildcard
                ? normalizedHost.EndsWith($".{this.Host}", StringComparison.Ordinal) && normalizedHost.Length > this.Host.Length + 1
                : normalizedHost.Equals(this.Host, StringComparison.Ordinal);
    }
}
