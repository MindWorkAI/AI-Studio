// ReSharper disable NotAccessedPositionalProperty.Local
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Asks the runtime to hand a file to the share sheet of the operating system.
    /// </summary>
    /// <param name="filePath">The plugin archive to share.</param>
    /// <returns>What became of the request. Any failure to reach the runtime counts as FAILED.</returns>
    public async Task<ShareFileResponse> ShareFile(string filePath)
    {
        try
        {
            using var response = await this.http.PostAsJsonAsync("/share/file", new ShareFileRequest(filePath), this.jsonRustSerializerOptions);
            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError($"The Rust runtime rejected the share request: {response.StatusCode}.");
                return new(ShareSheetOutcome.FAILED, $"The Rust runtime rejected the share request: {response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<ShareFileResponse>(this.jsonRustSerializerOptions);
            if (result is null)
            {
                this.logger?.LogError("The Rust runtime answered the share request without a result.");
                return new(ShareSheetOutcome.FAILED, "The Rust runtime answered the share request without a result.");
            }

            if (result.Outcome is not ShareSheetOutcome.OPENED)
                this.logger?.LogError($"The native share sheet did not open ({result.Outcome}): {result.Issue}");

            return result;
        }
        catch (HttpRequestException exception)
        {
            this.logger?.LogWarning(exception, "Failed to reach the Rust runtime share endpoint.");
            return new(ShareSheetOutcome.FAILED, exception.Message);
        }
        catch (TaskCanceledException exception)
        {
            this.logger?.LogWarning(exception, "Timed out while reaching the Rust runtime share endpoint.");
            return new(ShareSheetOutcome.FAILED, exception.Message);
        }
        catch (Exception exception)
        {
            this.logger?.LogError(exception, "Failed to process the Rust runtime share response.");
            return new(ShareSheetOutcome.FAILED, exception.Message);
        }
    }

    private sealed record ShareFileRequest(string FilePath);
}