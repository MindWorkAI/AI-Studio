using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;
using HtmlAgilityPack;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

public sealed class ReadWebPageTool(HTMLParser htmlParser, ILogger<ReadWebPageTool> logger) : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ReadWebPageTool).Namespace, nameof(ReadWebPageTool));

    private const int DEFAULT_TIMEOUT_SECONDS = 30;
    private const int DEFAULT_MAX_CONTENT_CHARACTERS = 12000;
    private const int MAX_TIMEOUT_SECONDS = 60;
    private const int MAX_CONTENT_CHARACTERS = 50000;
    private const int MAX_RESPONSE_BYTES = 5 * 1024 * 1024;
    private const int MAX_TRACE_LENGTH = 12000;
    private const string ALLOWED_PRIVATE_HOSTS_SETTING = "allowedPrivateHosts";

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

    public string GetDisplayName() => TB("Read Web Page");

    public string GetDescription() => TB("Load a single web page and extract its main HTML content.");

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "timeoutSeconds" => TB("Timeout Seconds"),
        "maxContentCharacters" => TB("Maximum Content Characters"),
        ALLOWED_PRIVATE_HOSTS_SETTING => TB("Allowed Private Hosts"),
        _ => TB(fieldDefinition.Title),
    };

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "timeoutSeconds" => TB("Optional HTTP timeout for loading a web page in seconds."),
        "maxContentCharacters" => TB("Optional global truncation limit for extracted characters returned to the model."),
        ALLOWED_PRIVATE_HOSTS_SETTING => TB("Optional host allowlist for private or VPN web pages. For security reasons, private or VPN web pages aren't allowed to be read by default. Separate host patterns with commas, such as example.de, *.example.de. Allowed private hosts require a high-confidence provider. For allowed internal hosts, AI Studio also tries the operating system's default sign-in automatically when the server responds with integrated authentication."),
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
        var triedOsSso = false;

        HTMLParserWebPage page;
        try
        {
            page = await htmlParser.LoadWebPageAsync(
                url,
                token,
                timeoutSeconds,
                async (candidateUrl, validationToken) => await this.ResolveValidatedUrlAddressesAsync(candidateUrl, allowedPrivateHosts, context.ProviderConfidence, validationToken),
                MAX_RESPONSE_BYTES,
                ExternalWebAuthenticationMode.OS_DEFAULT_CREDENTIALS,
                shouldUseDefaultCredentials: (candidateUrl, addresses) =>
                {
                    var shouldTryOsSso = ShouldTryOsSso(url, candidateUrl, addresses, allowedPrivateHosts, context.ProviderConfidence);
                    triedOsSso |= shouldTryOsSso;
                    return shouldTryOsSso;
                });
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException($"Loading the web page timed out after {timeoutSeconds} seconds.");
        }
        catch (HttpRequestException exception)
        {
            if (FindBlockedException(exception) is { } blockedException)
                throw blockedException;

            if (triedOsSso && exception.StatusCode is HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException(
                    $"Loading the web page failed: The server returned HTTP 401 (Unauthorized) for '{url}'. The host is reachable and AI Studio already tried your operating system's default sign-in, but the server did not accept it or requires an additional browser session/cookies.",
                    exception);
            }

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

    private static ToolExecutionBlockedException? FindBlockedException(Exception exception)
    {
        if (exception is ToolExecutionBlockedException blockedException)
            return blockedException;

        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                if (FindBlockedException(innerException) is { } innerBlockedException)
                    return innerBlockedException;
            }
        }

        return exception.InnerException is null ? null : FindBlockedException(exception.InnerException);
    }

    private async Task<IReadOnlyList<IPAddress>> ResolveValidatedUrlAddressesAsync(
        Uri url,
        IReadOnlyList<AllowedPrivateHostPattern> allowedPrivateHosts,
        ConfidenceLevel providerConfidence,
        CancellationToken token)
    {
        if (url is not { Scheme: "http" or "https" })
            throw new ToolExecutionBlockedException("Only HTTP and HTTPS URLs are supported.");

        if (IsBlockedHostName(url.Host))
            throw new ToolExecutionBlockedException("Local web page URLs are not supported.");

        var addresses = await ResolveHostAddressesAsync(url, token);
        if (addresses.Count == 0)
            throw new InvalidOperationException($"The host '{url.Host}' did not resolve to an IP address.");

        if (addresses.Any(IsNeverAllowedAddress))
            throw new ToolExecutionBlockedException("Local, link-local, multicast, and unspecified network addresses are not supported.");

        if (!addresses.Any(IsNonPublicAddress))
            return addresses;

        if (!IsAllowedPrivateHost(url.Host, allowedPrivateHosts))
            throw new ToolExecutionBlockedException("Private or local-network web page URLs are not supported unless their host is explicitly allowed.");

        if (providerConfidence >= ConfidenceLevel.HIGH)
            return addresses;

        await this.ReportPrivateHostProviderBlockAsync(url, providerConfidence);
        throw new ToolExecutionBlockedException("This private or VPN web page requires a High-confidence provider.");
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

    private static async Task<IReadOnlyList<IPAddress>> ResolveHostAddressesAsync(Uri url, CancellationToken token)
    {
        if (IPAddress.TryParse(url.Host, out var parsedAddress))
            return [NormalizeAddress(parsedAddress)];

        try
        {
            return (await Dns.GetHostAddressesAsync(url.DnsSafeHost, token))
                .Select(NormalizeAddress)
                .ToList();
        }
        catch (SocketException exception)
        {
            throw new InvalidOperationException($"The host '{url.Host}' could not be resolved: {exception.Message}", exception);
        }
    }

    private static IPAddress NormalizeAddress(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static bool IsBlockedHostName(string host)
    {
        var normalizedHost = NormalizeHost(host);
        return normalizedHost is "localhost" ||
               normalizedHost.EndsWith(".localhost", StringComparison.Ordinal);
    }

    private static bool IsAllowedPrivateHost(string host, IReadOnlyList<AllowedPrivateHostPattern> allowedPrivateHosts)
    {
        var normalizedHost = NormalizeHost(host);
        return allowedPrivateHosts.Any(pattern => pattern.IsMatch(normalizedHost));
    }

    private static bool ShouldTryOsSso(
        Uri originalUrl,
        Uri candidateUrl,
        IReadOnlyList<IPAddress> addresses,
        IReadOnlyList<AllowedPrivateHostPattern> allowedPrivateHosts,
        ConfidenceLevel providerConfidence) =>
        providerConfidence >= ConfidenceLevel.HIGH &&
        originalUrl.Scheme.Equals(candidateUrl.Scheme, StringComparison.OrdinalIgnoreCase) &&
        originalUrl.Host.Equals(candidateUrl.Host, StringComparison.OrdinalIgnoreCase) &&
        originalUrl.Port == candidateUrl.Port &&
        !IsBlockedHostName(candidateUrl.Host) &&
        IsAllowedPrivateHost(candidateUrl.Host, allowedPrivateHosts) &&
        addresses.Count > 0 &&
        addresses.All(IsNonPublicAddress);

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();

    private static bool IsNeverAllowedAddress(IPAddress address)
    {
        address = NormalizeAddress(address);
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily is AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return address.Equals(IPAddress.Any) ||
                   bytes[0] is 0 or 127 or >= 224 ||
                   (bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily is AddressFamily.InterNetworkV6)
        {
            return address.Equals(IPAddress.IPv6Any) ||
                   address.Equals(IPAddress.IPv6None) ||
                   address.Equals(IPAddress.IPv6Loopback) ||
                   address.IsIPv6LinkLocal ||
                   address.IsIPv6Multicast;
        }

        return true;
    }

    private static bool IsNonPublicAddress(IPAddress address)
    {
        address = NormalizeAddress(address);
        if (IsNeverAllowedAddress(address))
            return true;

        if (address.AddressFamily is AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 || // Private network: 10.0.0.0/8
                   (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) || // Carrier-grade NAT: 100.64.0.0/10
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || // Private network: 172.16.0.0/12
                   (bytes[0] == 192 && bytes[1] == 168) || // Private network: 192.168.0.0/16
                   (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) || // IETF protocol assignments: 192.0.0.0/24
                   (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) || // Documentation range: 192.0.2.0/24
                   (bytes[0] == 198 && bytes[1] is 18 or 19) || // Benchmark testing range: 198.18.0.0/15
                   (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) || // Documentation range: 198.51.100.0/24
                   (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113); // Documentation range: 203.0.113.0/24
        }

        if (address.AddressFamily is AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return (bytes[0] & 0xfe) == 0xfc || // Unique local addresses: fc00::/7
                   address.IsIPv6SiteLocal; // Deprecated site-local addresses: fec0::/10
        }

        return true;
    }

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

    private readonly record struct AllowedPrivateHostPattern(string Host, bool IsWildcard)
    {
        public bool IsMatch(string normalizedHost) =>
            this.IsWildcard
                ? normalizedHost.EndsWith($".{this.Host}", StringComparison.Ordinal) && normalizedHost.Length > this.Host.Length + 1
                : normalizedHost.Equals(this.Host, StringComparison.Ordinal);
    }
}
