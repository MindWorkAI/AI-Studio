using System.Text.Json;
using System.Text.Json.Nodes;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

public sealed class SearXNGWebSearchTool : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool));

    private readonly SearXNGSearchClient searchClient = new();
    private readonly SearXNGPageRetrievalService pageRetrievalService;

    private const int DEFAULT_MAX_RESULTS = 5;
    private const int DEFAULT_TIMEOUT_SECONDS = 20;
    private const int MAX_RESULTS = 20;
    private const int MAX_PAGE = 20;
    private const int MAX_TIMEOUT_SECONDS = 60;
    private const int MAX_TRACE_LENGTH = 4000;
    private const int DEFAULT_MAX_TOTAL_CONTENT_CHARACTERS = 100000;
    private const int DEFAULT_MIN_CONTENT_CHARACTERS_PER_RESULT = 3000;
    private const int DEFAULT_PAGE_TIMEOUT_SECONDS = 30;
    private const int DEFAULT_RETRIEVAL_TIMEOUT_SECONDS = 90;
    private const int MAX_TOTAL_CONTENT_CHARACTERS = 100000;
    private const int MAX_MIN_CONTENT_CHARACTERS_PER_RESULT = 3000;
    private const int MAX_PAGE_TIMEOUT_SECONDS = 30;
    private const int MAX_RETRIEVAL_TIMEOUT_SECONDS = 90;

    public SearXNGWebSearchTool(WebPageRetrievalService webPageRetrievalService)
    {
        this.pageRetrievalService = new SearXNGPageRetrievalService(webPageRetrievalService);
    }

    public string ImplementationKey => ToolSelectionRules.WEB_SEARCH_TOOL_ID;

    public string Icon => Icons.Material.Filled.Language;

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public string GetDisplayName() => TB("Web Search");

    public string GetDescription() => TB("Search the web with a configured SearXNG instance and retrieve the readable content of the best matching pages.");

    public string GetSettingsFieldLabel(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "baseUrl" => TB("SearXNG URL"),
        "defaultLanguage" => TB("Default Language"),
        "defaultSafeSearch" => TB("Default Safe Search"),
        "defaultCategories" => TB("Default Categories"),
        "defaultEngines" => TB("Default Engines"),
        "maxResults" => TB("Maximum Results"),
        "timeoutSeconds" => TB("Timeout Seconds"),
        "maxTotalContentCharacters" => TB("Maximum Total Content Characters"),
        "minContentCharactersPerResult" => TB("Minimum Content Characters Per Result"),
        "pageTimeoutSeconds" => TB("Page Timeout Seconds"),
        "retrievalTimeoutSeconds" => TB("Retrieval Timeout Seconds"),
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
        "maxTotalContentCharacters" => TB("Optional total character budget shared by all retrieved pages."),
        "minContentCharactersPerResult" => TB("Optional minimum character budget reserved for each successfully retrieved page."),
        "pageTimeoutSeconds" => TB("Optional timeout for loading each individual result page in seconds."),
        "retrievalTimeoutSeconds" => TB("Optional overall timeout for retrieving all result pages in seconds."),
        _ => TB(fieldDefinition.Description),
    };

    public string? GetSettingsFieldDefaultValue(string fieldName, ToolSettingsFieldDefinition fieldDefinition) => fieldName switch
    {
        "maxResults" => DEFAULT_MAX_RESULTS.ToString(),
        "timeoutSeconds" => DEFAULT_TIMEOUT_SECONDS.ToString(),
        "maxTotalContentCharacters" => DEFAULT_MAX_TOTAL_CONTENT_CHARACTERS.ToString(),
        "minContentCharactersPerResult" => DEFAULT_MIN_CONTENT_CHARACTERS_PER_RESULT.ToString(),
        "pageTimeoutSeconds" => DEFAULT_PAGE_TIMEOUT_SECONDS.ToString(),
        "retrievalTimeoutSeconds" => DEFAULT_RETRIEVAL_TIMEOUT_SECONDS.ToString(),
        _ => null,
    };

    public Task<ToolConfigurationState?> ValidateConfigurationAsync(
        ToolDefinition definition,
        IReadOnlyDictionary<string, string> settingsValues,
        CancellationToken token = default)
    {
        var positiveIntegerErrorFormat = TB("The setting '{0}' must be a positive integer.");
        var maximumErrorFormat = TB("The setting '{0}' must be less than or equal to {1}.");
        settingsValues.TryGetValue("baseUrl", out var baseUrl);
        if (!TryNormalizeSearchUri(baseUrl ?? string.Empty, out _, out var uriError))
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

        if (!ToolSettingsValueParser.TryReadOptionalPositiveInt(settingsValues, "maxResults", positiveIntegerErrorFormat, out _, out var maxResultsError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = maxResultsError,
            });
        }

        if (!ToolSettingsValueParser.TryReadOptionalPositiveInt(settingsValues, "timeoutSeconds", positiveIntegerErrorFormat, out _, out var timeoutError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = timeoutError,
            });
        }

        if (!ToolSettingsValueParser.TryReadBoundedOptionalPositiveInt(settingsValues, "maxTotalContentCharacters", MAX_TOTAL_CONTENT_CHARACTERS, positiveIntegerErrorFormat, maximumErrorFormat, out var maxTotalContentCharacters, out var maxTotalContentError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = maxTotalContentError,
            });
        }

        if (!ToolSettingsValueParser.TryReadBoundedOptionalPositiveInt(settingsValues, "minContentCharactersPerResult", MAX_MIN_CONTENT_CHARACTERS_PER_RESULT, positiveIntegerErrorFormat, maximumErrorFormat, out var minContentCharactersPerResult, out var minContentError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = minContentError,
            });
        }

        if (!ToolSettingsValueParser.TryReadBoundedOptionalPositiveInt(settingsValues, "pageTimeoutSeconds", MAX_PAGE_TIMEOUT_SECONDS, positiveIntegerErrorFormat, maximumErrorFormat, out _, out var pageTimeoutError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = pageTimeoutError,
            });
        }

        if (!ToolSettingsValueParser.TryReadBoundedOptionalPositiveInt(settingsValues, "retrievalTimeoutSeconds", MAX_RETRIEVAL_TIMEOUT_SECONDS, positiveIntegerErrorFormat, maximumErrorFormat, out _, out var retrievalTimeoutError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = retrievalTimeoutError,
            });
        }

        var effectiveMaxTotalContentCharacters = maxTotalContentCharacters ?? DEFAULT_MAX_TOTAL_CONTENT_CHARACTERS;
        var effectiveMinContentCharactersPerResult = minContentCharactersPerResult ?? DEFAULT_MIN_CONTENT_CHARACTERS_PER_RESULT;
        if (effectiveMaxTotalContentCharacters < effectiveMinContentCharactersPerResult * MAX_RESULTS)
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = string.Format(TB("The total content budget must reserve at least {0} characters for each of up to {1} results."), effectiveMinContentCharactersPerResult, MAX_RESULTS),
            });
        }

        return Task.FromResult<ToolConfigurationState?>(null);
    }

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default)
    {
        context.SettingsValues.TryGetValue("baseUrl", out var baseUrl);
        if (!TryNormalizeSearchUri(baseUrl ?? string.Empty, out var searchUri, out var uriError))
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

        var defaultLimit = ToolSettingsValueParser.ReadOptionalPositiveInt(context.SettingsValues, "maxResults") ?? DEFAULT_MAX_RESULTS;
        var effectiveLimit = Math.Min(requestedLimit ?? defaultLimit, MAX_RESULTS);
        var timeoutSeconds = Math.Min(ToolSettingsValueParser.ReadOptionalPositiveInt(context.SettingsValues, "timeoutSeconds") ?? DEFAULT_TIMEOUT_SECONDS, MAX_TIMEOUT_SECONDS);
        var maxTotalContentCharacters = Math.Min(ToolSettingsValueParser.ReadOptionalPositiveInt(context.SettingsValues, "maxTotalContentCharacters") ?? DEFAULT_MAX_TOTAL_CONTENT_CHARACTERS, MAX_TOTAL_CONTENT_CHARACTERS);
        var minContentCharactersPerResult = Math.Min(ToolSettingsValueParser.ReadOptionalPositiveInt(context.SettingsValues, "minContentCharactersPerResult") ?? DEFAULT_MIN_CONTENT_CHARACTERS_PER_RESULT, MAX_MIN_CONTENT_CHARACTERS_PER_RESULT);
        var pageTimeoutSeconds = Math.Min(ToolSettingsValueParser.ReadOptionalPositiveInt(context.SettingsValues, "pageTimeoutSeconds") ?? DEFAULT_PAGE_TIMEOUT_SECONDS, MAX_PAGE_TIMEOUT_SECONDS);
        var retrievalTimeoutSeconds = Math.Min(ToolSettingsValueParser.ReadOptionalPositiveInt(context.SettingsValues, "retrievalTimeoutSeconds") ?? DEFAULT_RETRIEVAL_TIMEOUT_SECONDS, MAX_RETRIEVAL_TIMEOUT_SECONDS);
        if (maxTotalContentCharacters < minContentCharactersPerResult * MAX_RESULTS)
            throw new InvalidOperationException(TB("The configured web search content budget is not valid."));
        if (page is > MAX_PAGE)
            throw new ArgumentException($"Argument 'page' must be less than or equal to {MAX_PAGE}.");

        var searchResponse = await this.searchClient.SearchAsync(
            new SearXNGSearchRequest(
                searchUri,
                query,
                categories,
                engines,
                language,
                timeRange,
                page,
                safeSearch,
                effectiveLimit,
                timeoutSeconds),
            token);
        var retrievalResult = await this.pageRetrievalService.RetrieveAsync(
            searchResponse.Candidates,
            pageTimeoutSeconds,
            retrievalTimeoutSeconds,
            maxTotalContentCharacters,
            minContentCharactersPerResult,
            token);

        var resultArray = new JsonArray();
        foreach (var result in retrievalResult.Results)
            resultArray.Add(BuildResultJson(result));

        var resultObject = new JsonObject
        {
            ["candidate_count"] = searchResponse.CandidateCount,
            ["result_count"] = retrievalResult.Results.Count,
            ["retrieval_timed_out"] = retrievalResult.RetrievalTimedOut,
            ["results"] = resultArray,
        };
        if (retrievalResult.Results.Count == 0)
            resultObject["diagnostic"] = "No result page could be retrieved as readable public HTML. Pages may have failed, timed out, been blocked by network safety checks, used an unsupported content type, or contained no readable static content.";

        return new ToolExecutionResult
        {
            JsonContent = resultObject
        };
    }

    public string FormatTraceResult(string rawResult)
    {
        if (rawResult.Length <= MAX_TRACE_LENGTH)
            return rawResult;

        return $"{rawResult[..MAX_TRACE_LENGTH]}...";
    }

    private static JsonObject BuildResultJson(WebSearchPageResult result)
    {
        var extractedPage = result.RetrievedPage.ExtractedPage;
        var page = result.RetrievedPage.Page;
        var originalContentCharacters = extractedPage.Markdown.Length;
        var searchMetadata = new JsonObject
        {
            ["rank"] = result.Candidate.Rank,
            ["requested_url"] = page.RequestedUrl.ToString(),
            ["final_url"] = page.FinalUrl.ToString(),
            ["engines"] = BuildJsonArray(result.Candidate.Engines),
            ["published_date"] = result.Candidate.PublishedDate,
        };
        var pageContent = new JsonObject
        {
            ["status"] = result.ContentTruncated || originalContentCharacters < 500 ? "partial or truncated" : "complete",
            ["title"] = extractedPage.Title,
            ["description"] = extractedPage.Description,
            ["authors"] = BuildJsonArray(extractedPage.Authors),
            ["content"] = result.ReturnedMarkdown,
        };

        return new JsonObject
        {
            ["search_metadata"] = searchMetadata,
            ["page"] = pageContent,
        };
    }

    private static JsonArray BuildJsonArray(IEnumerable<string> values)
    {
        var result = new JsonArray();
        foreach (var value in values)
            result.Add(value);
        return result;
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

    private static List<string> SplitCommaSeparatedValues(string? value) => value?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.Ordinal)
        .ToList() ?? [];

    private static bool TryNormalizeSearchUri(string rawUrl, out Uri searchUri, out string error) =>
        SearXNGSearchClient.TryNormalizeSearchUri(
            rawUrl,
            TB("A SearXNG URL is required."),
            TB("The configured SearXNG URL is not a valid absolute URL."),
            TB("The configured SearXNG URL must start with http:// or https://."),
            out searchUri,
            out error);
}
