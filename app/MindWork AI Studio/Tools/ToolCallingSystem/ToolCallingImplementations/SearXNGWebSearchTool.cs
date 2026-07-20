using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIStudio.Tools;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

public sealed class SearXNGWebSearchTool(WebPageRetrievalService webPageRetrievalService) : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SearXNGWebSearchTool).Namespace, nameof(SearXNGWebSearchTool));

    private const int DEFAULT_MAX_RESULTS = 5;
    private const int DEFAULT_TIMEOUT_SECONDS = 20;
    private const int MAX_RESULTS = 20;
    private const int MAX_PAGE = 20;
    private const int MAX_TIMEOUT_SECONDS = 60;
    private const int MAX_RESPONSE_BYTES = 1024 * 1024;
    private const int MAX_TRACE_LENGTH = 4000;
    private const int DEFAULT_MAX_TOTAL_CONTENT_CHARACTERS = 100000;
    private const int DEFAULT_MIN_CONTENT_CHARACTERS_PER_RESULT = 3000;
    private const int DEFAULT_PAGE_TIMEOUT_SECONDS = 30;
    private const int DEFAULT_RETRIEVAL_TIMEOUT_SECONDS = 90;
    private const int MAX_TOTAL_CONTENT_CHARACTERS = 100000;
    private const int MAX_MIN_CONTENT_CHARACTERS_PER_RESULT = 3000;
    private const int MAX_PAGE_TIMEOUT_SECONDS = 30;
    private const int MAX_RETRIEVAL_TIMEOUT_SECONDS = 90;
    private const int MAX_PARALLEL_RETRIEVALS = 4;

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

        if (!TryReadBoundedOptionalPositiveInt(settingsValues, "maxTotalContentCharacters", MAX_TOTAL_CONTENT_CHARACTERS, out var maxTotalContentCharacters, out var maxTotalContentError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = maxTotalContentError,
            });
        }

        if (!TryReadBoundedOptionalPositiveInt(settingsValues, "minContentCharactersPerResult", MAX_MIN_CONTENT_CHARACTERS_PER_RESULT, out var minContentCharactersPerResult, out var minContentError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = minContentError,
            });
        }

        if (!TryReadBoundedOptionalPositiveInt(settingsValues, "pageTimeoutSeconds", MAX_PAGE_TIMEOUT_SECONDS, out _, out var pageTimeoutError))
        {
            return Task.FromResult<ToolConfigurationState?>(new ToolConfigurationState
            {
                IsConfigured = false,
                Message = pageTimeoutError,
            });
        }

        if (!TryReadBoundedOptionalPositiveInt(settingsValues, "retrievalTimeoutSeconds", MAX_RETRIEVAL_TIMEOUT_SECONDS, out _, out var retrievalTimeoutError))
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
        var effectiveLimit = Math.Min(requestedLimit ?? defaultLimit, MAX_RESULTS);
        var timeoutSeconds = Math.Min(ReadOptionalPositiveIntSetting(context.SettingsValues, "timeoutSeconds") ?? DEFAULT_TIMEOUT_SECONDS, MAX_TIMEOUT_SECONDS);
        var maxTotalContentCharacters = Math.Min(ReadOptionalPositiveIntSetting(context.SettingsValues, "maxTotalContentCharacters") ?? DEFAULT_MAX_TOTAL_CONTENT_CHARACTERS, MAX_TOTAL_CONTENT_CHARACTERS);
        var minContentCharactersPerResult = Math.Min(ReadOptionalPositiveIntSetting(context.SettingsValues, "minContentCharactersPerResult") ?? DEFAULT_MIN_CONTENT_CHARACTERS_PER_RESULT, MAX_MIN_CONTENT_CHARACTERS_PER_RESULT);
        var pageTimeoutSeconds = Math.Min(ReadOptionalPositiveIntSetting(context.SettingsValues, "pageTimeoutSeconds") ?? DEFAULT_PAGE_TIMEOUT_SECONDS, MAX_PAGE_TIMEOUT_SECONDS);
        var retrievalTimeoutSeconds = Math.Min(ReadOptionalPositiveIntSetting(context.SettingsValues, "retrievalTimeoutSeconds") ?? DEFAULT_RETRIEVAL_TIMEOUT_SECONDS, MAX_RETRIEVAL_TIMEOUT_SECONDS);
        if (maxTotalContentCharacters < minContentCharactersPerResult * MAX_RESULTS)
            throw new InvalidOperationException(TB("The configured web search content budget is not valid."));
        if (page is > MAX_PAGE)
            throw new ArgumentException($"Argument 'page' must be less than or equal to {MAX_PAGE}.");

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

        using var httpClient = ExternalHttpClientTimeout.CreateHttpClient(searchUri, ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED);
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(searchUri, queryParameters));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var response = await SendAsync(httpClient, request, timeoutCts.Token, timeoutSeconds, token);
        var responseBody = await ReadContentAsStringWithLimitAsync(response.Content, MAX_RESPONSE_BYTES, timeoutCts.Token);
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

        var candidates = BuildCandidates(responseObject["results"] as JsonArray, effectiveLimit, out var candidateCount);
        var attemptedCount = 0;
        var retrievalTimedOut = 0;
        using var retrievalTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        retrievalTimeoutCts.CancelAfter(TimeSpan.FromSeconds(retrievalTimeoutSeconds));
        using var retrievalSemaphore = new SemaphoreSlim(MAX_PARALLEL_RETRIEVALS);

        async Task<RetrievedSearchPage?> RetrieveCandidateAsync(SearchCandidate candidate)
        {
            var enteredSemaphore = false;
            try
            {
                await retrievalSemaphore.WaitAsync(retrievalTimeoutCts.Token);
                enteredSemaphore = true;
                Interlocked.Increment(ref attemptedCount);
                var retrievedPage = await webPageRetrievalService.RetrieveAsync(
                    candidate.RetrievalUrl,
                    new WebPageRetrievalOptions
                    {
                        TimeoutSeconds = pageTimeoutSeconds,
                        PublicTargetsOnly = true,
                    },
                    retrievalTimeoutCts.Token);
                if (string.IsNullOrWhiteSpace(retrievedPage.ExtractedPage.Markdown))
                    return null;

                return new RetrievedSearchPage(candidate, retrievedPage);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                Interlocked.Exchange(ref retrievalTimedOut, 1);
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (enteredSemaphore)
                    retrievalSemaphore.Release();
            }
        }

        var retrievedPages = await Task.WhenAll(candidates.Select(RetrieveCandidateAsync));
        token.ThrowIfCancellationRequested();
        var mergedResults = MergeFinalUrlDuplicates(retrievedPages.OfType<RetrievedSearchPage>());
        ApplyContentBudget(mergedResults, maxTotalContentCharacters, minContentCharactersPerResult);
        var resultArray = new JsonArray();
        foreach (var result in mergedResults)
            resultArray.Add(BuildResultJson(result));

        var resultObject = new JsonObject
        {
            // ["query"] = query,
            ["candidate_count"] = candidateCount,
            // ["attempted_count"] = attemptedCount,
            ["result_count"] = mergedResults.Count,
            // ["omitted_count"] = Math.Max(0, candidateCount - mergedResults.Count),
            ["retrieval_timed_out"] = retrievalTimedOut == 1,
            ["results"] = resultArray,
        };
        if (mergedResults.Count == 0)
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

    private static List<SearchCandidate> BuildCandidates(JsonArray? resultArray, int effectiveLimit, out int candidateCount)
    {
        var resultObjects = resultArray?.OfType<JsonObject>().ToList() ?? [];
        var hasSortableScores = resultObjects.Any(result => TryGetScore(result, out _));
        IEnumerable<JsonObject> orderedResults = hasSortableScores
            ? resultObjects
                .OrderByDescending(result => TryGetScore(result, out var score) ? score : double.MinValue)
                .ThenBy(result => result["title"]?.ToString(), StringComparer.OrdinalIgnoreCase)
            : resultObjects;
        var rankedResults = orderedResults
            .Take(effectiveLimit)
            .ToList();
        candidateCount = rankedResults.Count;

        var candidatesByUrl = new Dictionary<string, SearchCandidate>(StringComparer.Ordinal);
        for (var index = 0; index < rankedResults.Count; index++)
        {
            var result = rankedResults[index];
            var originalUrl = ReadNodeString(result["url"]);
            if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var url) || url is not { Scheme: "http" or "https" })
                continue;

            var retrievalUrl = RemoveFragment(url);
            var candidate = new SearchCandidate
            {
                Rank = index + 1,
                RetrievalUrl = retrievalUrl,
                OriginalUrls = [originalUrl],
                Title = ReadNodeString(result["title"]),
                Snippet = ReadNodeString(result["content"]),
                Engines = ReadStringValues(result, "engine", "engines"),
                Categories = ReadStringValues(result, "category", "categories"),
                PublishedDate = FirstNonEmpty(ReadNodeString(result["publishedDate"]), ReadNodeString(result["published_date"])),
            };
            var normalizedUrl = NormalizeUrl(retrievalUrl);
            if (candidatesByUrl.TryGetValue(normalizedUrl, out var existingCandidate))
                existingCandidate.Merge(candidate);
            else
                candidatesByUrl[normalizedUrl] = candidate;
        }

        return candidatesByUrl.Values
            .OrderBy(candidate => candidate.Rank)
            .ToList();
    }

    private static List<SearchResult> MergeFinalUrlDuplicates(IEnumerable<RetrievedSearchPage> retrievedPages) => retrievedPages
        .GroupBy(result => NormalizeUrl(result.RetrievedPage.Page.FinalUrl), StringComparer.Ordinal)
        .Select(group =>
        {
            var rankedGroup = group.OrderBy(result => result.Candidate.Rank).ToList();
            var metadata = rankedGroup[0].Candidate.Clone();
            foreach (var duplicate in rankedGroup.Skip(1))
                metadata.Merge(duplicate.Candidate);

            return new SearchResult(metadata, rankedGroup[0].RetrievedPage);
        })
        .OrderBy(result => result.Candidate.Rank)
        .ToList();

    private static void ApplyContentBudget(List<SearchResult> results, int maxTotalContentCharacters, int minContentCharactersPerResult)
    {
        var remainingBudget = maxTotalContentCharacters;
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var originalMarkdown = result.RetrievedPage.ExtractedPage.Markdown;
            var remainingResults = results.Count - index - 1;
            var currentBudget = remainingBudget - minContentCharactersPerResult * remainingResults;
            if (originalMarkdown.Length > currentBudget)
            {
                result.ReturnedMarkdown = MarkdownTruncator.Truncate(originalMarkdown, currentBudget);
                result.ContentTruncated = true;
            }
            else
            {
                result.ReturnedMarkdown = originalMarkdown;
            }

            remainingBudget -= result.ReturnedMarkdown.Length;
        }
    }

    private static JsonObject BuildResultJson(SearchResult result)
    {
        var extractedPage = result.RetrievedPage.ExtractedPage;
        var page = result.RetrievedPage.Page;
        var originalContentCharacters = extractedPage.Markdown.Length;
        var searchMetadata = new JsonObject
        {
            ["rank"] = result.Candidate.Rank,
            ["requested_url"] = page.RequestedUrl.ToString(),
            ["final_url"] = page.FinalUrl.ToString(),
            // ["title"] = result.Candidate.Title,
            // ["snippet"] = result.Candidate.Snippet,
            ["engines"] = BuildJsonArray(result.Candidate.Engines),
            // ["categories"] = BuildJsonArray(result.Candidate.Categories),
            ["published_date"] = result.Candidate.PublishedDate,
        };
        var pageContent = new JsonObject
        {
            // ["url"] = page.RequestedUrl.ToString(),
            // ["retrieved_at_utc"] = result.RetrievedPage.RetrievedAtUtc.ToString("O"),
            ["status"] = result.ContentTruncated || originalContentCharacters < 500 ? "partial or truncated" : "complete",
            ["title"] = extractedPage.Title,
            ["description"] = extractedPage.Description,
            ["authors"] = BuildJsonArray(extractedPage.Authors),
            ["content"] = result.ReturnedMarkdown,
            // ["language"] = extractedPage.Language,
            // ["published_time"] = extractedPage.PublishedTime,
            // ["modified_time"] = extractedPage.ModifiedTime,
            // ["media_type"] = page.ContentType,
            // ["content_truncated"] = result.ContentTruncated,
            // ["original_content_characters"] = originalContentCharacters,
            // ["returned_content_characters"] = result.ReturnedMarkdown.Length,
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

    private static List<string> ReadStringValues(JsonObject source, string singularPropertyName, string pluralPropertyName)
    {
        var values = new List<string>();
        AddNodeStringValues(source[singularPropertyName], values);
        AddNodeStringValues(source[pluralPropertyName], values);
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddNodeStringValues(JsonNode? node, List<string> values)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
                AddNodeStringValues(item, values);
            return;
        }

        var value = ReadNodeString(node);
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value);
    }

    private static string ReadNodeString(JsonNode? node) => node is null ? string.Empty : node.ToString().Trim();

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static Uri RemoveFragment(Uri url) => new UriBuilder(url)
    {
        Fragment = string.Empty,
    }.Uri;

    private static string NormalizeUrl(Uri url)
    {
        var scheme = url.Scheme.ToLowerInvariant();
        var host = url.IdnHost.TrimEnd('.').ToLowerInvariant();
        var port = url.IsDefaultPort ? string.Empty : $":{url.Port}";
        var userInfo = string.IsNullOrEmpty(url.UserInfo) ? string.Empty : $"{url.UserInfo}@";
        return $"{scheme}://{userInfo}{host}{port}{url.AbsolutePath}{url.Query}";
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

    private static async Task<string> ReadContentAsStringWithLimitAsync(HttpContent content, int maxResponseBytes, CancellationToken token)
    {
        if (content.Headers.ContentLength is long contentLength && contentLength > maxResponseBytes)
            throw new InvalidOperationException($"The SearXNG response body is too large. Maximum allowed size is {maxResponseBytes} bytes.");

        await using var stream = await content.ReadAsStreamAsync(token);
        await using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, token);
            if (read == 0)
                break;

            if (buffer.Length + read > maxResponseBytes)
                throw new InvalidOperationException($"The SearXNG response body is too large. Maximum allowed size is {maxResponseBytes} bytes.");

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
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

    private static bool TryReadBoundedOptionalPositiveInt(
        IReadOnlyDictionary<string, string> settingsValues,
        string key,
        int maximum,
        out int? value,
        out string error)
    {
        if (!TryReadOptionalPositiveInt(settingsValues, key, out value, out error))
            return false;

        if (value is null || value <= maximum)
            return true;

        error = string.Format(TB("The setting '{0}' must be less than or equal to {1}."), key, maximum);
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"The SearXNG request failed: {exception.Message}", exception);
        }
    }

    private sealed class SearchCandidate
    {
        public required int Rank { get; set; }

        public required Uri RetrievalUrl { get; set; }

        public required List<string> OriginalUrls { get; init; }

        public required string Title { get; set; }

        public required string Snippet { get; set; }

        public required List<string> Engines { get; init; }

        public required List<string> Categories { get; init; }

        public required string PublishedDate { get; set; }

        public SearchCandidate Clone() => new()
        {
            Rank = this.Rank,
            RetrievalUrl = this.RetrievalUrl,
            OriginalUrls = [..this.OriginalUrls],
            Title = this.Title,
            Snippet = this.Snippet,
            Engines = [..this.Engines],
            Categories = [..this.Categories],
            PublishedDate = this.PublishedDate,
        };

        public void Merge(SearchCandidate candidate)
        {
            if (candidate.Rank < this.Rank)
            {
                this.Rank = candidate.Rank;
                this.RetrievalUrl = candidate.RetrievalUrl;
                this.Title = candidate.Title;
                this.Snippet = candidate.Snippet;
                this.PublishedDate = candidate.PublishedDate;
            }
            else
            {
                this.Title = FirstNonEmpty(this.Title, candidate.Title);
                this.Snippet = FirstNonEmpty(this.Snippet, candidate.Snippet);
                this.PublishedDate = FirstNonEmpty(this.PublishedDate, candidate.PublishedDate);
            }

            AddDistinct(this.OriginalUrls, candidate.OriginalUrls, StringComparer.Ordinal);
            AddDistinct(this.Engines, candidate.Engines, StringComparer.OrdinalIgnoreCase);
            AddDistinct(this.Categories, candidate.Categories, StringComparer.OrdinalIgnoreCase);
        }

        private static void AddDistinct(List<string> target, IEnumerable<string> values, StringComparer comparer)
        {
            foreach (var value in values)
            {
                if (!target.Contains(value, comparer))
                    target.Add(value);
            }
        }
    }

    private sealed record RetrievedSearchPage(SearchCandidate Candidate, RetrievedWebPage RetrievedPage);

    private sealed class SearchResult(SearchCandidate candidate, RetrievedWebPage retrievedPage)
    {
        public SearchCandidate Candidate { get; } = candidate;

        public RetrievedWebPage RetrievedPage { get; } = retrievedPage;

        public string ReturnedMarkdown { get; set; } = string.Empty;

        public bool ContentTruncated { get; set; }
    }
}
