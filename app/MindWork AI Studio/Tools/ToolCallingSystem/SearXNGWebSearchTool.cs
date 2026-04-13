using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class SearXNGWebSearchTool : IToolImplementation
{
    private const int DEFAULT_MAX_RESULTS = 5;
    private const int DEFAULT_TIMEOUT_SECONDS = 20;
    private const int MAX_TRACE_LENGTH = 4000;

    public string ImplementationKey => "web_search";

    public string Icon => Icons.Material.Filled.Language;

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public string GetDisplayName() => I18N.I.T("Web Search", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool));

    public string GetDescription() => I18N.I.T("Search the web with a configured SearXNG instance and return structured JSON results for the model. When deeper content is needed, use Read Web Page on relevant result URLs.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool));

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "baseUrl" => I18N.I.T("SearXNG URL", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "defaultLanguage" => I18N.I.T("Default Language", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "defaultSafeSearch" => I18N.I.T("Default Safe Search", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "defaultCategories" => I18N.I.T("Default Categories", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "defaultEngines" => I18N.I.T("Default Engines", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "maxResults" => I18N.I.T("Maximum Results", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "timeoutSeconds" => I18N.I.T("Timeout Seconds", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        _ => I18N.I.T(fieldDefinition.Title, typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
    };

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "baseUrl" => I18N.I.T("Base URL of the SearXNG instance. You can enter either the instance root URL or the /search endpoint.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "defaultLanguage" => I18N.I.T("Optional fallback language code when the model does not provide a language.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "defaultSafeSearch" => I18N.I.T("Optional safe search policy sent to SearXNG when configured.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "defaultCategories" => I18N.I.T("Optional comma-separated default categories. Do not set this together with default engines.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "defaultEngines" => I18N.I.T("Optional comma-separated default engines. Do not set this together with default categories.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "maxResults" => I18N.I.T("Optional default maximum number of results returned to the model when the model does not provide a limit.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        "timeoutSeconds" => I18N.I.T("Optional HTTP timeout for the search request in seconds.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
        _ => I18N.I.T(fieldDefinition.Description, typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
    };

    public Task<ToolConfigurationState?> ValidateConfigurationAsync(
        ToolDefinition definition,
        IReadOnlyDictionary<string, string> settingsValues,
        CancellationToken token = default)
    {
        settingsValues.TryGetValue("baseUrl", out var baseUrl);
        var isValidBaseUrl = TryNormalizeSearchUri(baseUrl ?? string.Empty, out _, out var uriError);
        if (!isValidBaseUrl)
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = uriError,
            });
        }

        var hasDefaultCategories = !string.IsNullOrWhiteSpace(settingsValues.GetValueOrDefault("defaultCategories"));
        var hasDefaultEngines = !string.IsNullOrWhiteSpace(settingsValues.GetValueOrDefault("defaultEngines"));
        if (hasDefaultCategories && hasDefaultEngines)
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = I18N.I.T("Default categories and default engines cannot both be set for the web search tool.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)),
            });
        }

        if (!TryReadOptionalPositiveInt(settingsValues, "maxResults", out _, out var maxResultsError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = maxResultsError,
            });
        }

        if (!TryReadOptionalPositiveInt(settingsValues, "timeoutSeconds", out _, out var timeoutError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = timeoutError,
            });
        }

        return Task.FromResult<ToolConfigurationState?>(null);
    }

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default)
    {
        context.SettingsValues.TryGetValue("baseUrl", out var baseUrl);
        var isValidBaseUrl = TryNormalizeSearchUri(baseUrl ?? string.Empty, out var searchUri, out var uriError);
        if (!isValidBaseUrl)
            throw new InvalidOperationException(uriError);

        var query = ReadRequiredString(arguments, "query");
        var categories = ReadOptionalStringArray(arguments, "categories");
        var engines = ReadOptionalStringArray(arguments, "engines");
        var language = ReadOptionalString(arguments, "language");
        var timeRange = ReadOptionalString(arguments, "time_range");
        var page = ReadOptionalPositiveInt(arguments, "page");
        var requestedLimit = ReadOptionalPositiveInt(arguments, "limit");

        if (timeRange is not null && timeRange is not ("day" or "month" or "year"))
            throw new ArgumentException($"Invalid time_range '{timeRange}'.");

        language = string.IsNullOrWhiteSpace(language) ? context.SettingsValues.GetValueOrDefault("defaultLanguage") : language;
        var safeSearch = context.SettingsValues.GetValueOrDefault("defaultSafeSearch");

        if (categories.Count == 0)
            categories = SplitCommaSeparatedValues(context.SettingsValues.GetValueOrDefault("defaultCategories"));

        if (engines.Count == 0)
            engines = SplitCommaSeparatedValues(context.SettingsValues.GetValueOrDefault("defaultEngines"));

        if (categories.Count > 0 && engines.Count > 0 && !string.IsNullOrWhiteSpace(context.SettingsValues.GetValueOrDefault("defaultCategories")) && !string.IsNullOrWhiteSpace(context.SettingsValues.GetValueOrDefault("defaultEngines")))
            throw new InvalidOperationException(I18N.I.T("Default categories and default engines cannot both be set for the web search tool.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool)));

        var defaultLimit = ReadOptionalPositiveIntSetting(context.SettingsValues, "maxResults") ?? DEFAULT_MAX_RESULTS;
        var effectiveLimit = requestedLimit ?? defaultLimit;
        var timeoutSeconds = ReadOptionalPositiveIntSetting(context.SettingsValues, "timeoutSeconds") ?? DEFAULT_TIMEOUT_SECONDS;

        var queryParameters = new List<KeyValuePair<string, string>>
        {
            new("q", query),
            new("format", "json"),
        };

        if (categories.Count > 0)
            queryParameters.Add(new KeyValuePair<string, string>("categories", string.Join(",", categories)));

        if (engines.Count > 0)
            queryParameters.Add(new KeyValuePair<string, string>("engines", string.Join(",", engines)));

        if (!string.IsNullOrWhiteSpace(language))
            queryParameters.Add(new KeyValuePair<string, string>("language", language));

        if (!string.IsNullOrWhiteSpace(timeRange))
            queryParameters.Add(new KeyValuePair<string, string>("time_range", timeRange));

        if (page is not null)
            queryParameters.Add(new KeyValuePair<string, string>("pageno", page.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(safeSearch))
            queryParameters.Add(new KeyValuePair<string, string>("safesearch", safeSearch));

        using var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(searchUri, queryParameters));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var response = await SendAsync(httpClient, request, timeoutCts.Token, timeoutSeconds, token);
        var responseBody = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
        {
            var responseDetails = string.IsNullOrWhiteSpace(responseBody) ? string.Empty : $" Response body: {responseBody[..Math.Min(responseBody.Length, 400)]}";
            throw new InvalidOperationException($"The SearXNG request failed with status code {(int)response.StatusCode} ({response.StatusCode}).{responseDetails}");
        }

        JsonNode? responseJson;
        try
        {
            responseJson = JsonNode.Parse(responseBody);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The SearXNG response was not valid JSON: {exception.Message}", exception);
        }

        if (responseJson is not JsonObject responseObject)
            throw new InvalidOperationException("The SearXNG response JSON must be an object.");

        var resultArray = responseObject["results"] as JsonArray;
        if (resultArray is not null && resultArray.Count > effectiveLimit)
        {
            var truncatedResults = new JsonArray();
            foreach (var result in resultArray.Take(effectiveLimit))
                truncatedResults.Add(result?.DeepClone());

            responseObject["results"] = truncatedResults;
        }

        var requestJson = new JsonObject
        {
            ["query"] = query,
            ["format"] = "json",
            ["limit"] = effectiveLimit,
        };

        if (categories.Count > 0)
            requestJson["categories"] = BuildJsonArray(categories);

        if (engines.Count > 0)
            requestJson["engines"] = BuildJsonArray(engines);

        if (!string.IsNullOrWhiteSpace(language))
            requestJson["language"] = language;

        if (!string.IsNullOrWhiteSpace(timeRange))
            requestJson["time_range"] = timeRange;

        if (page is not null)
            requestJson["page"] = page.Value;

        if (!string.IsNullOrWhiteSpace(safeSearch))
            requestJson["safesearch"] = safeSearch;

        return new ToolExecutionResult
        {
            JsonContent = new JsonObject
            {
                ["request"] = requestJson,
                ["response"] = responseObject,
            },
        };
    }

    public string FormatTraceResult(string rawResult)
    {
        if (rawResult.Length <= MAX_TRACE_LENGTH)
            return rawResult;

        return $"{rawResult[..MAX_TRACE_LENGTH]}...";
    }

    private static string ReadRequiredString(JsonElement arguments, string propertyName)
    {
        var value = ReadOptionalString(arguments, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required argument '{propertyName}'.");

        return value;
    }

    private static string? ReadOptionalString(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString()?.Trim(),
            _ => throw new ArgumentException($"Argument '{propertyName}' must be a string."),
        };
    }

    private static int? ReadOptionalPositiveInt(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind is JsonValueKind.Null)
            return null;

        if (value.ValueKind is not JsonValueKind.Number || !value.TryGetInt32(out var intValue) || intValue <= 0)
            throw new ArgumentException($"Argument '{propertyName}' must be a positive integer.");

        return intValue;
    }

    private static List<string> ReadOptionalStringArray(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null)
            return [];

        if (value.ValueKind is not JsonValueKind.Array)
            throw new ArgumentException($"Argument '{propertyName}' must be an array of strings.");

        var values = new List<string>();
        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind is not JsonValueKind.String)
                throw new ArgumentException($"Argument '{propertyName}' must be an array of strings.");

            var item = element.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(item))
                values.Add(item);
        }

        return values;
    }

    private static JsonArray BuildJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);

        return array;
    }

    private static List<string> SplitCommaSeparatedValues(string? value) => value?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.Ordinal)
        .ToList() ?? [];

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

        error = I18N.I.T($"The setting '{key}' must be a positive integer.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool));
        return false;
    }

    private static bool TryNormalizeSearchUri(string rawUrl, out Uri searchUri, out string error)
    {
        searchUri = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            error = I18N.I.T("A SearXNG URL is required.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool));
            return false;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var parsedUri))
        {
            error = I18N.I.T("The configured SearXNG URL is not a valid absolute URL.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool));
            return false;
        }

        if (parsedUri.Scheme is not ("http" or "https"))
        {
            error = I18N.I.T("The configured SearXNG URL must start with http:// or https://.", typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool));
            return false;
        }

        var basePath = parsedUri.AbsolutePath.TrimEnd('/');
        if (basePath.EndsWith("/search", StringComparison.OrdinalIgnoreCase))
            basePath = basePath[..^"/search".Length];

        var normalizedPath = $"{basePath}/search";
        var builder = new UriBuilder(parsedUri)
        {
            Path = normalizedPath,
            Query = string.Empty,
            Fragment = string.Empty,
        };
        searchUri = builder.Uri;
        return true;
    }

    private static Uri BuildRequestUri(Uri searchUri, IEnumerable<KeyValuePair<string, string>> queryParameters)
    {
        var builder = new StringBuilder();
        foreach (var parameter in queryParameters)
        {
            if (builder.Length > 0)
                builder.Append('&');

            builder.Append(WebUtility.UrlEncode(parameter.Key));
            builder.Append('=');
            builder.Append(WebUtility.UrlEncode(parameter.Value));
        }

        var uriBuilder = new UriBuilder(searchUri)
        {
            Query = builder.ToString(),
        };
        return uriBuilder.Uri;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        CancellationToken requestToken,
        int timeoutSeconds,
        CancellationToken callerToken)
    {
        try
        {
            return await httpClient.SendAsync(request, requestToken);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The SearXNG request timed out after {timeoutSeconds} seconds.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"The SearXNG request failed: {exception.Message}", exception);
        }
    }
}
