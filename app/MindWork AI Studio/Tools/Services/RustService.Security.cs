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
}