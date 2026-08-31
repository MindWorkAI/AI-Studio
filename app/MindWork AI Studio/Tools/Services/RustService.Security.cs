using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// How long one sanitize request may take.
    /// </summary>
    /// <remarks>
    /// Web pages and retrieval contexts are small, so this only exists to keep a stuck runtime
    /// from blocking the caller forever.
    /// </remarks>
    private static readonly TimeSpan SANITIZE_TIMEOUT = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long one batch of sanitize requests may take.
    /// </summary>
    /// <remarks>
    /// A batch carries every text of one tool call, such as all pages of a web search, so it is
    /// given more room than a single text.
    /// </remarks>
    private static readonly TimeSpan SANITIZE_BATCH_TIMEOUT = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Asks the runtime to filter prompt injections out of a text.
    /// </summary>
    /// <remarks>
    /// File content does not go through here: the runtime filters it while it streams the file.
    /// This is the path for content the app fetched itself, i.e. web pages and retrieval contexts.
    /// </remarks>
    /// <param name="text">The content to filter.</param>
    /// <returns>The filtered content and what was found or null when the runtime could not be reached.</returns>
    public async Task<SanitizePromptInjectionsResponse?> SanitizePromptInjections(string text)
    {
        try
        {
            using var timeoutTokenSource = new CancellationTokenSource(SANITIZE_TIMEOUT);
            using var response = await this.http.PostAsJsonAsync(
                "/security/prompt-injection/sanitize",
                new SanitizePromptInjectionsRequest(text),
                cancellationToken: timeoutTokenSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError("Failed to check a text for prompt injections. Status: {StatusCode}, reason: '{ReasonPhrase}'", response.StatusCode, response.ReasonPhrase);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SanitizePromptInjectionsResponse>(timeoutTokenSource.Token);
        }
        catch (Exception exception)
        {
            this.logger?.LogError(exception, "Failed to check a text for prompt injections.");
            return null;
        }
    }

    /// <summary>
    /// Asks the runtime to filter prompt injections out of several texts in one request.
    /// </summary>
    /// <remarks>
    /// One tool call can produce many texts at once: a web search returns several pages, each
    /// with its own content, title, description, and authors. Sending them together saves a
    /// round trip per field.
    /// </remarks>
    /// <param name="texts">The contents to filter.</param>
    /// <returns>
    /// One result per text, in the same order, or null when the runtime could not be reached or
    /// answered with a different number of results than were requested. Callers match results to
    /// their texts by index, so a mismatched answer is unusable rather than partially usable.
    /// </returns>
    public async Task<IReadOnlyList<SanitizePromptInjectionsResponse>?> SanitizePromptInjectionsBatch(IReadOnlyList<string> texts)
    {
        if (texts.Count is 0)
            return [];

        try
        {
            using var timeoutTokenSource = new CancellationTokenSource(SANITIZE_BATCH_TIMEOUT);
            using var response = await this.http.PostAsJsonAsync(
                "/security/prompt-injection/sanitize-batch",
                new SanitizePromptInjectionsBatchRequest(texts),
                cancellationToken: timeoutTokenSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError("Failed to check {TextCount} text(s) for prompt injections. Status: {StatusCode}, reason: '{ReasonPhrase}'", texts.Count, response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var batchResponse = await response.Content.ReadFromJsonAsync<SanitizePromptInjectionsBatchResponse>(timeoutTokenSource.Token);
            if (batchResponse.Results is null || batchResponse.Results.Count != texts.Count)
            {
                this.logger?.LogError("The prompt injection filter answered with {ResultCount} result(s) for {TextCount} text(s).", batchResponse.Results?.Count ?? 0, texts.Count);
                return null;
            }

            return batchResponse.Results;
        }
        catch (Exception exception)
        {
            this.logger?.LogError(exception, "Failed to check {TextCount} text(s) for prompt injections.", texts.Count);
            return null;
        }
    }
}