using System.Text.Json;
using System.Text.Json.Nodes;
using AIStudio.Tools.PluginSystem;
using HtmlAgilityPack;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

public sealed class ReadWebPageTool(HTMLParser htmlParser) : IToolImplementation
{
    private const int DEFAULT_TIMEOUT_SECONDS = 30;
    private const int DEFAULT_MAX_CONTENT_CHARACTERS = 12000;
    private const int MAX_TRACE_LENGTH = 12000;

    private static readonly string[] REMOVED_NODE_XPATHS =
    [
        "//script",
        "//style",
        "//noscript",
        "//nav",
        "//footer",
        "//aside",
        "//form",
        "//iframe",
        "//*[@role='navigation']",
        "//*[@role='contentinfo']",
        "//*[@role='complementary']"
    ];

    public string ImplementationKey => ToolSelectionRules.READ_WEB_PAGE_TOOL_ID;

    public string Icon => Icons.Material.Filled.Article;

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public string GetDisplayName() => I18N.I.T("Read Web Page", typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool));

    public string GetDescription() => I18N.I.T("Load a single web page, extract its main HTML content, and return structured working material for the model. Use the result to synthesize a natural-language answer instead of exposing the raw payload to the user.", typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool));

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "timeoutSeconds" => I18N.I.T("Timeout Seconds", typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool)),
        "maxContentCharacters" => I18N.I.T("Maximum Content Characters", typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool)),
        _ => I18N.I.T(fieldDefinition.Title, typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool)),
    };

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "timeoutSeconds" => I18N.I.T("Optional HTTP timeout for loading a web page in seconds.", typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool)),
        "maxContentCharacters" => I18N.I.T("Optional global truncation limit for extracted Markdown returned to the model.", typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool)),
        _ => I18N.I.T(fieldDefinition.Description, typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool)),
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

        return Task.FromResult<ToolConfigurationState?>(null);
    }

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default)
    {
        var urlText = ReadRequiredString(arguments, "url");
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var url) || url is not { Scheme: "http" or "https" })
            throw new ArgumentException("Argument 'url' must be a valid HTTP or HTTPS URL.");

        var timeoutSeconds = ReadOptionalPositiveIntSetting(context.SettingsValues, "timeoutSeconds") ?? DEFAULT_TIMEOUT_SECONDS;
        var maxContentCharacters = ReadOptionalPositiveIntSetting(context.SettingsValues, "maxContentCharacters") ?? DEFAULT_MAX_CONTENT_CHARACTERS;

        HTMLParserWebPage page;
        try
        {
            page = await htmlParser.LoadWebPageAsync(url, token, timeoutSeconds);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException($"Loading the web page timed out after {timeoutSeconds} seconds.");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"Loading the web page failed: {exception.Message}", exception);
        }

        if (!IsSupportedHtmlContentType(page.ContentType))
            throw new InvalidOperationException($"Unsupported content type '{page.ContentType}'. Only HTML pages are supported.");

        var document = page.Document;
        var title = htmlParser.ExtractTitle(document);
        var contentRoot = document.DocumentNode.SelectSingleNode("//article") ??
                          document.DocumentNode.SelectSingleNode("//main") ??
                          document.DocumentNode.SelectSingleNode("//body") ??
                          document.DocumentNode;

        RemoveNoiseNodes(contentRoot);

        var markdown = htmlParser.ParseToMarkdown(contentRoot.InnerHtml).Trim();
        var warnings = new JsonArray();
        if (string.IsNullOrWhiteSpace(title))
            warnings.Add("No title could be extracted from the page.");

        if (string.IsNullOrWhiteSpace(markdown))
            warnings.Add("The extracted page content is empty.");
        else if (markdown.Length < 200)
            warnings.Add("The extracted page content is very short and may be incomplete.");

        if (markdown.Length > maxContentCharacters)
        {
            markdown = markdown[..maxContentCharacters].TrimEnd();
            warnings.Add($"The extracted page content was truncated to {maxContentCharacters} characters.");
        }

        return new ToolExecutionResult
        {
            JsonContent = BuildResponseJson(page, title, markdown, warnings)
        };
    }

    private static JsonObject BuildResponseJson(HTMLParserWebPage page, string title, string markdown, JsonArray warnings)
    {
        var response = new JsonObject
        {
            ["metadata"] = new JsonObject
            {
                ["url"] = page.RequestedUrl.ToString(),
                ["final_url"] = page.FinalUrl.ToString(),
                ["title"] = title,
            },
            ["content_markdown"] = markdown,
        };

        if (warnings.Count > 0)
            response["warnings"] = warnings;

        return response;
    }

    public string FormatTraceResult(string rawResult)
    {
        if (rawResult.Length <= MAX_TRACE_LENGTH)
            return rawResult;

        return $"{rawResult[..MAX_TRACE_LENGTH]}...";
    }

    private static void RemoveNoiseNodes(HtmlNode rootNode)
    {
        foreach (var xpath in REMOVED_NODE_XPATHS)
        {
            var nodes = rootNode.SelectNodes(xpath);
            if (nodes is null)
                continue;

            foreach (var node in nodes.ToList())
                node.Remove();
        }
    }

    private static bool IsSupportedHtmlContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ||
        contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);

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
}
