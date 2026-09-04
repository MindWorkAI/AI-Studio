using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIStudio.Tools;
using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

internal sealed class SearXNGSearchClient
{
    private const int MAX_RESPONSE_BYTES = 1024 * 1024;

    public async Task<SearXNGSearchResponse> SearchAsync(SearXNGSearchRequest searchRequest, CancellationToken token)
    {
        try
        {
            return await SearchInternalAsync(searchRequest, token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutException or InvalidOperationException or JsonException)
        {
            //
            // The reason has to travel with the message. It reaches the user through the tool
            // trace and the model through the tool result, and neither can act on "it failed":
            // a disabled JSON API, a bot check, and a rate limit all need different answers.
            //
            throw new InvalidOperationException($"The SearXNG search request failed: {exception.Message}", exception);
        }
    }

    private static async Task<SearXNGSearchResponse> SearchInternalAsync(SearXNGSearchRequest searchRequest, CancellationToken token)
    {
        var queryParameters = new List<KeyValuePair<string, string>>
        {
            new("q", searchRequest.Query),
            new("format", "json"),
        };

        if (!string.IsNullOrWhiteSpace(searchRequest.Language))
            queryParameters.Add(new KeyValuePair<string, string>("language", searchRequest.Language));

        if (!string.IsNullOrWhiteSpace(searchRequest.TimeRange))
            queryParameters.Add(new KeyValuePair<string, string>("time_range", searchRequest.TimeRange));

        if (searchRequest.Page is not null)
            queryParameters.Add(new KeyValuePair<string, string>("pageno", searchRequest.Page.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(searchRequest.SafeSearch))
            queryParameters.Add(new KeyValuePair<string, string>("safesearch", searchRequest.SafeSearch));

        using var httpClient = ExternalHttpClientTimeout.CreateHttpClient(searchRequest.SearchUri, ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED);
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(searchRequest.SearchUri, queryParameters));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(searchRequest.TimeoutSeconds));

        using var response = await SendAsync(httpClient, request, timeoutCts.Token, searchRequest.TimeoutSeconds, token);
        var responseBody = await HttpContentReader.ReadAsStringWithLimitAsync(response.Content, MAX_RESPONSE_BYTES, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            var responseExcerpt = CreateSingleLineExcerpt(responseBody);
            var responseDetails = string.IsNullOrWhiteSpace(responseExcerpt) ? string.Empty : $" Response body: {responseExcerpt}";
            var statusHint = response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => " The instance rate-limits this client. Public instances usually do that for automated requests; a self-hosted instance does not.",
                HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => " The instance refused the request. It may have the JSON format disabled, or it requires authentication or a bot check.",
                _ => string.Empty,
            };

            throw new InvalidOperationException($"The SearXNG request failed with status code {(int)response.StatusCode} ({response.StatusCode}).{statusHint}{responseDetails}");
        }

        //
        // A SearXNG instance that does not serve the JSON API answers the HTML page instead —
        // and some answer a bot check that way, with a success status code. Without this test the
        // failure surfaces as a JSON syntax error, which points at the wrong thing entirely.
        //
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) && !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The SearXNG instance answered '{mediaType}' instead of JSON. Enable the JSON format in the instance's settings.yml ('search.formats' must contain 'json'). Most public instances do not serve it and put a bot check or rate limit in front of automated requests. Response body: {CreateSingleLineExcerpt(responseBody)}");
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

        var candidates = BuildCandidates(responseObject["results"] as JsonArray, searchRequest.EffectiveLimit, out var candidateCount);
        return new SearXNGSearchResponse(candidates, candidateCount, ReadUnresponsiveEngines(responseObject["unresponsive_engines"] as JsonArray));
    }

    private static string CreateSingleLineExcerpt(string responseBody)
    {
        var sanitizedResponseBody = string.Concat(responseBody.Select(character => char.IsControl(character) ? ' ' : character));
        var excerpt = string.Join(" ", sanitizedResponseBody
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return excerpt[..Math.Min(excerpt.Length, 400)];
    }

    public static bool TryNormalizeSearchUri(
        string rawUrl,
        string requiredUrlError,
        string invalidAbsoluteUrlError,
        string unsupportedSchemeError,
        out Uri searchUri,
        out string error)
    {
        searchUri = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            error = requiredUrlError;
            return false;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var parsedUri))
        {
            error = invalidAbsoluteUrlError;
            return false;
        }

        if (parsedUri.Scheme is not ("http" or "https"))
        {
            error = unsupportedSchemeError;
            return false;
        }

        var basePath = parsedUri.AbsolutePath.TrimEnd('/');
        if (basePath.EndsWith("/search", StringComparison.OrdinalIgnoreCase))
            basePath = basePath[..^"/search".Length];

        var builder = new UriBuilder(parsedUri)
        {
            Path = $"{basePath}/search",
            Query = string.Empty,
            Fragment = string.Empty,
        };
        searchUri = builder.Uri;
        return true;
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

    /// <summary>
    /// Reads which search engines did not answer, and why.
    /// </summary>
    /// <remarks>
    /// SearXNG reports these as pairs of engine name and reason. They are the difference between
    /// "nothing matches this query" and "the instance has no working engines", which is the usual
    /// state of a fresh instance whose engines answer with a CAPTCHA or time out. Without them a
    /// misconfigured instance is indistinguishable from an obscure query.
    /// </remarks>
    private static IReadOnlyList<string> ReadUnresponsiveEngines(JsonArray? unresponsiveEngines)
    {
        if (unresponsiveEngines is null)
            return [];

        var engines = new List<string>();
        foreach (var entry in unresponsiveEngines)
        {
            switch (entry)
            {
                case JsonArray { Count: > 0 } pair:
                    var engineName = ReadNodeString(pair[0]);
                    var reason = pair.Count > 1 ? ReadNodeString(pair[1]) : string.Empty;
                    if (!string.IsNullOrWhiteSpace(engineName))
                        engines.Add(string.IsNullOrWhiteSpace(reason) ? engineName : $"{engineName} ({reason})");

                    break;

                // Older SearXNG versions report a plain name instead of a pair:
                case not null when !string.IsNullOrWhiteSpace(ReadNodeString(entry)):
                    engines.Add(ReadNodeString(entry));
                    break;
            }
        }

        return engines;
    }

    private static string ReadNodeString(JsonNode? node) => node is null ? string.Empty : node.ToString().Trim();

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

    /// <remarks>
    /// Two cancellation tokens, so one of them cannot be the last parameter: the request token
    /// carries the search timeout, while the caller token says the user gave up. Telling them
    /// apart is what turns a cancellation into either a timeout message or a silent abort.
    /// </remarks>
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
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"The SearXNG request failed: {exception.Message}", exception);
        }
    }

    internal static string NormalizeUrl(Uri url)
    {
        var scheme = url.Scheme.ToLowerInvariant();
        var host = url.IdnHost.TrimEnd('.').ToLowerInvariant();
        var port = url.IsDefaultPort ? string.Empty : $":{url.Port}";
        var userInfo = string.IsNullOrEmpty(url.UserInfo) ? string.Empty : $"{url.UserInfo}@";
        return $"{scheme}://{userInfo}{host}{port}{url.AbsolutePath}{url.Query}";
    }

    internal static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static Uri RemoveFragment(Uri url) => new UriBuilder(url)
    {
        Fragment = string.Empty,
    }.Uri;
}