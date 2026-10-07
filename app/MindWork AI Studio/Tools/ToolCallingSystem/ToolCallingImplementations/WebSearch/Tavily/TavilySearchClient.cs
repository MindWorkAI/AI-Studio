using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Tavily;

/// <summary>
/// Talks to Tavily's search API.
/// </summary>
/// <remarks>
/// Nothing here knows the tool's settings or its result shape: the client sends one request,
/// hands back what Tavily answered, and turns a failure into a message that says what to do
/// about it. How a search becomes Tavily's parameters is the backend's part.
/// </remarks>
internal sealed class TavilySearchClient
{
    private const string SEARCH_URL = "https://api.tavily.com/search";

    private const int MAX_RESPONSE_BYTES = 1024 * 1024;

    /// <summary>
    /// The month's included requests are used up, or the key has reached its own quota.
    /// </summary>
    /// <remarks>
    /// Not a status code the framework knows, hence the cast. Tavily uses this range to separate
    /// an exhausted budget from a rate limit, which is the difference between waiting a moment
    /// and waiting until next month.
    /// </remarks>
    private const HttpStatusCode PLAN_LIMIT_STATUS_CODE = (HttpStatusCode)432;

    /// <summary>
    /// The spending limit of a pay-as-you-go account is reached.
    /// </summary>
    private const HttpStatusCode PAY_AS_YOU_GO_LIMIT_STATUS_CODE = (HttpStatusCode)433;

    public async Task<TavilySearchResponse> SearchAsync(string apiKey, TavilySearchRequest searchRequest, int timeoutSeconds, CancellationToken token)
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
            // a rejected key, an exhausted budget, and a rate limit all need different answers.
            //
            throw new InvalidOperationException($"The Tavily search request failed: {exception.Message}", exception);
        }
    }

    private static async Task<TavilySearchResponse> SearchInternalAsync(string apiKey, TavilySearchRequest searchRequest, int timeoutSeconds, CancellationToken token)
    {
        var searchUri = new Uri(SEARCH_URL);

        //
        // Tavily is a public service on the internet, so its certificate has to come from a root
        // the system trusts. Custom roots exist for a self-hosted search instance behind a
        // company's own certificate authority, which this is not:
        //
        using var httpClient = ExternalHttpClientTimeout.CreateHttpClient(searchUri, ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY);
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
        using var request = new HttpRequestMessage(HttpMethod.Post, searchUri);
        request.Content = JsonContent.Create(searchRequest, options: WebSearchJson.OPTIONS);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var response = await SendAsync(httpClient, request, timeoutCts.Token, timeoutSeconds, token);
        var responseBody = await HttpContentReader.ReadAsStringWithLimitAsync(response.Content, MAX_RESPONSE_BYTES, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildStatusCodeMessage(response.StatusCode, responseBody));

        TavilySearchResponse? searchResponse;
        try
        {
            searchResponse = JsonSerializer.Deserialize<TavilySearchResponse>(responseBody, WebSearchJson.OPTIONS);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The Tavily response was not valid JSON: {exception.Message}", exception);
        }

        if (searchResponse is null)
            throw new InvalidOperationException("Tavily answered with an empty response body.");

        return searchResponse;
    }

    /// <summary>
    /// What a refused request means, in words the user and the model can act on.
    /// </summary>
    /// <remarks>
    /// An exhausted budget is what makes these hints worth having: it is the expected end of the
    /// free requests of a month, and without the hint it would read as a broken search service
    /// and send the user looking for a fault that is not there.
    /// </remarks>
    private static string BuildStatusCodeMessage(HttpStatusCode statusCode, string responseBody)
    {
        var statusHint = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => " Tavily refused the API key. Check whether the key is complete and still active.",
            PLAN_LIMIT_STATUS_CODE => " The included Tavily requests of this month are used up, or this API key has reached the quota set for it. Searching works again next month, or with a higher plan.",
            PAY_AS_YOU_GO_LIMIT_STATUS_CODE => " The spending limit of the Tavily account is reached. Raising it in the Tavily account allows searching again.",
            HttpStatusCode.TooManyRequests => " Tavily rate-limits this API key. Wait a moment before searching again.",
            HttpStatusCode.BadRequest => " Tavily rejected the parameters of the request.",

            _ => string.Empty,
        };

        return $"Tavily answered with status code {(int)statusCode} ({statusCode}).{statusHint}{SearchResponseExcerpt.CreateDetails(responseBody)}";
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
            throw new TimeoutException($"The Tavily request timed out after {timeoutSeconds} seconds.");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"The Tavily request failed: {exception.Message}", exception);
        }
    }
}