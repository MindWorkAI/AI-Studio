using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;

/// <summary>
/// Talks to Staan's search API.
/// </summary>
/// <remarks>
/// Nothing here knows the tool's settings or its result shape: the client sends one request,
/// hands back what Staan answered, and turns a failure into a message that says what to do
/// about it. How a search becomes Staan's parameters is the backend's part.
/// </remarks>
internal sealed class StaanSearchClient
{
    private const string SEARCH_URL = "https://api.staan.ai/v2/search/web";

    private const int MAX_RESPONSE_BYTES = 1024 * 1024;

    public async Task<StaanSearchResponse> SearchAsync(string apiKey, StaanSearchRequest searchRequest, int timeoutSeconds, CancellationToken token)
    {
        try
        {
            return await SearchInternalAsync(apiKey, searchRequest, timeoutSeconds, token);
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
            // a rejected key, an exhausted quota, and a rate limit all need different answers.
            //
            throw new InvalidOperationException($"The Staan search request failed: {exception.Message}", exception);
        }
    }

    private static async Task<StaanSearchResponse> SearchInternalAsync(string apiKey, StaanSearchRequest searchRequest, int timeoutSeconds, CancellationToken token)
    {
        var searchUri = new Uri(SEARCH_URL);

        //
        // Staan is a public service on the internet, so its certificate has to come from a root
        // the system trusts. Custom roots exist for a self-hosted search instance behind a
        // company's own certificate authority, which this is not:
        //
        using var httpClient = ExternalHttpClientTimeout.CreateHttpClient(searchUri, ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY);
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
        using var request = new HttpRequestMessage(HttpMethod.Post, searchUri)
        {
            Content = JsonContent.Create(searchRequest, options: WebSearchJson.OPTIONS),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var response = await SendAsync(httpClient, request, timeoutCts.Token, timeoutSeconds, token);
        var responseBody = await HttpContentReader.ReadAsStringWithLimitAsync(response.Content, MAX_RESPONSE_BYTES, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildStatusCodeMessage(response.StatusCode, responseBody));

        StaanSearchResponse? searchResponse;
        try
        {
            searchResponse = JsonSerializer.Deserialize<StaanSearchResponse>(responseBody, WebSearchJson.OPTIONS);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The Staan response was not valid JSON: {exception.Message}", exception);
        }

        if (searchResponse is null)
            throw new InvalidOperationException("Staan answered with an empty response body.");

        return searchResponse;
    }

    /// <summary>
    /// What a refused request means, in words the user and the model can act on.
    /// </summary>
    /// <remarks>
    /// An exhausted quota is the one worth naming: it is the expected end of the free searches,
    /// and without the hint it would read as a broken search service.
    /// </remarks>
    private static string BuildStatusCodeMessage(HttpStatusCode statusCode, string responseBody)
    {
        var statusHint = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => " Staan refused the API key. Check whether the key is complete and still active.",
            HttpStatusCode.PaymentRequired => " The Staan account has no searches left. The free requests are used up, and paid usage has to be set up to continue.",
            HttpStatusCode.TooManyRequests => " Staan rate-limits this API key. Wait a moment before searching again.",
            HttpStatusCode.BadRequest => " Staan rejected the parameters of the request.",
            _ => string.Empty,
        };

        return $"Staan answered with status code {(int)statusCode} ({statusCode}).{statusHint}{SearchResponseExcerpt.CreateDetails(responseBody)}";
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
            throw new TimeoutException($"The Staan request timed out after {timeoutSeconds} seconds.");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"The Staan request failed: {exception.Message}", exception);
        }
    }
}