using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIStudio.Tools;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

internal sealed class SearXNGSearchClient
{
    private const int MAX_RESPONSE_BYTES = 1024 * 1024;

    public async Task<SearXNGSearchResponse> SearchAsync(SearXNGSearchRequest searchRequest, CancellationToken token)
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

        var candidates = BuildCandidates(responseObject["results"] as JsonArray, searchRequest.EffectiveLimit, out var candidateCount);
        return new SearXNGSearchResponse(candidates, candidateCount);
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

internal sealed record SearXNGSearchRequest(
    Uri SearchUri,
    string Query,
    string? Language,
    string? TimeRange,
    int? Page,
    string? SafeSearch,
    int EffectiveLimit,
    int TimeoutSeconds);

internal sealed record SearXNGSearchResponse(IReadOnlyList<SearchCandidate> Candidates, int CandidateCount);

internal sealed class SearchCandidate
{
    public required int Rank { get; set; }

    public required Uri RetrievalUrl { get; set; }

    public required List<string> OriginalUrls { get; init; }

    public required string Title { get; set; }

    public required string Snippet { get; set; }

    public required string PublishedDate { get; set; }

    public SearchCandidate Clone() => new()
    {
        Rank = this.Rank,
        RetrievalUrl = this.RetrievalUrl,
        OriginalUrls = [..this.OriginalUrls],
        Title = this.Title,
        Snippet = this.Snippet,
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
            this.Title = SearXNGSearchClient.FirstNonEmpty(this.Title, candidate.Title);
            this.Snippet = SearXNGSearchClient.FirstNonEmpty(this.Snippet, candidate.Snippet);
            this.PublishedDate = SearXNGSearchClient.FirstNonEmpty(this.PublishedDate, candidate.PublishedDate);
        }

        AddDistinct(this.OriginalUrls, candidate.OriginalUrls, StringComparer.Ordinal);
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
