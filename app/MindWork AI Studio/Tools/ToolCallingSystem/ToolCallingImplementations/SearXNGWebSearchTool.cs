using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

public sealed class SearXNGWebSearchTool : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool));

    private const int DEFAULT_MAX_RESULTS = 5;
    private const int DEFAULT_TIMEOUT_SECONDS = 20;
    private const int MAX_TRACE_LENGTH = 4000;

    public string ImplementationKey => "web_search";

    public string Icon => Icons.Material.Filled.Language;

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public string GetDisplayName() => TB("Web Search");

    public string GetDescription() => TB("Search the web with a configured SearXNG instance and return candidate URLs for the model. Use Read Web Page on relevant result URLs before answering factual or detailed web questions.");

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "baseUrl" => TB("SearXNG URL"),
        "defaultLanguage" => TB("Default Language"),
        "defaultSafeSearch" => TB("Default Safe Search"),
        "defaultCategories" => TB("Default Categories"),
        "defaultEngines" => TB("Default Engines"),
        "maxResults" => TB("Maximum Results"),
        "timeoutSeconds" => TB("Timeout Seconds"),
        _ => TB(fieldDefinition.Title),
    };

    public string GetSettingsFieldDescription(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "baseUrl" => TB("Base URL of the SearXNG instance. You can enter either the instance root URL or the /search endpoint."),
        "defaultLanguage" => TB("Optional fallback language code when the model does not provide a language."),
        "defaultSafeSearch" => TB("Optional safe search policy sent to SearXNG when configured."),
        "defaultCategories" => TB("Optional comma-separated default categories. Do not set this together with default engines."),
        "defaultEngines" => TB("Optional comma-separated default engines. Do not set this together with default categories."),
        "maxResults" => TB("Optional default maximum number of results returned to the model when the model does not provide a limit."),
        "timeoutSeconds" => TB("Optional HTTP timeout for the search request in seconds."),
        _ => TB(fieldDefinition.Description),
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
                Message = TB("Default categories and default engines cannot both be set for the web search tool."),
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
            throw new InvalidOperationException(TB("Default categories and default engines cannot both be set for the web search tool."));

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

        responseObject = SanitizeResponse(responseObject, effectiveLimit);

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

    private static JsonObject SanitizeResponse(JsonObject responseObject, int effectiveLimit)
    {
        var sanitizedResponse = new JsonObject();

        var resultArray = responseObject["results"] as JsonArray;
        var sanitizedResults = BuildSanitizedResults(resultArray, effectiveLimit);
        sanitizedResponse["results"] = sanitizedResults;

        var suggestions = BuildSuggestions(responseObject["suggestions"] as JsonArray);
        if (suggestions.Count > 0)
            sanitizedResponse["suggestions"] = suggestions;

        return sanitizedResponse;
    }

    private static JsonArray BuildSanitizedResults(JsonArray? resultArray, int effectiveLimit)
    {
        var sanitizedResults = new JsonArray();
        if (resultArray is null)
            return sanitizedResults;

        var resultObjects = resultArray.OfType<JsonObject>().ToList();
        var hasSortableScores = resultObjects.Any(result => TryGetScore(result, out _));
        IEnumerable<JsonObject> orderedResults = hasSortableScores
            ? resultObjects
                .OrderByDescending(result => TryGetScore(result, out var score) ? score : double.MinValue)
                .ThenBy(result => result["title"]?.ToString(), StringComparer.OrdinalIgnoreCase)
            : resultObjects;

        foreach (var result in orderedResults.Take(effectiveLimit))
            sanitizedResults.Add(SanitizeResult(result));

        return sanitizedResults;
    }

    private static JsonObject SanitizeResult(JsonObject result)
    {
        var sanitizedResult = new JsonObject();
        CopyPropertyIfPresent(result, sanitizedResult, "title");
        CopyPropertyIfPresent(result, sanitizedResult, "url");
        CopyPropertyIfPresent(result, sanitizedResult, "content");
        CopyPropertyIfPresent(result, sanitizedResult, "score");
        CopyPropertyIfPresent(result, sanitizedResult, "engine");
        CopyPropertyIfPresent(result, sanitizedResult, "category");
        CopyPropertyIfPresent(result, sanitizedResult, "publishedDate");
        CopyPropertyIfPresent(result, sanitizedResult, "published_date");

        return sanitizedResult;
    }

    private static JsonArray BuildSuggestions(JsonArray? suggestionsArray)
    {
        var suggestions = new JsonArray();
        if (suggestionsArray is null)
            return suggestions;

        foreach (var suggestionNode in suggestionsArray.Take(3))
        {
            var suggestion = suggestionNode switch
            {
                JsonValue value => value.TryGetValue<string>(out var stringSuggestion) ? stringSuggestion : null,
                JsonObject suggestionObject when suggestionObject.TryGetPropertyValue("suggestion", out var suggestionValue) => suggestionValue?.ToString(),
                JsonObject suggestionObject when suggestionObject.TryGetPropertyValue("title", out var titleValue) => titleValue?.ToString(),
                _ => suggestionNode?.ToString(),
            };

            if (!string.IsNullOrWhiteSpace(suggestion))
                suggestions.Add(suggestion);
        }

        return suggestions;
    }

    private static void CopyPropertyIfPresent(JsonObject source, JsonObject target, string propertyName)
    {
        if (source.TryGetPropertyValue(propertyName, out var propertyValue) && propertyValue is not null)
            target[propertyName] = propertyValue.DeepClone();
    }

    private static bool TryGetScore(JsonObject result, out double score)
    {
        score = double.MinValue;
        if (!result.TryGetPropertyValue("score", out var scoreNode) || scoreNode is null)
            return false;

        return scoreNode switch
        {
            JsonValue value when value.TryGetValue<double>(out var doubleScore) => ReturnScore(doubleScore, out score),
            JsonValue value when value.TryGetValue<decimal>(out var decimalScore) => ReturnScore((double)decimalScore, out score),
            JsonValue value when value.TryGetValue<int>(out var intScore) => ReturnScore(intScore, out score),
            _ => double.TryParse(scoreNode.ToString(), out var parsedScore) && ReturnScore(parsedScore, out score),
        };
    }

    private static bool ReturnScore(double input, out double score)
    {
        score = input;
        return true;
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

        error = string.Format(TB("The setting '{0}' must be a positive integer."), key);
        return false;
    }

    private static bool TryNormalizeSearchUri(string rawUrl, out Uri searchUri, out string error)
    {
        searchUri = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            error = TB("A SearXNG URL is required.");
            return false;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var parsedUri))
        {
            error = TB("The configured SearXNG URL is not a valid absolute URL.");
            return false;
        }

        if (parsedUri.Scheme is not ("http" or "https"))
        {
            error = TB("The configured SearXNG URL must start with http:// or https://.");
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
