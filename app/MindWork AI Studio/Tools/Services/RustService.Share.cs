namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<bool> ShareFile(string filePath)
    {
        try
        {
            using var response = await this.http.PostAsJsonAsync(
                "/share/file",
                new ShareFileRequest(filePath),
                this.jsonRustSerializerOptions);
            if (!response.IsSuccessStatusCode)
            {
                this.logger?.LogError($"The Rust runtime rejected the share request: {response.StatusCode}.");
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<ShareFileResponse>(this.jsonRustSerializerOptions);
            if (result?.Success == true)
                return true;

            this.logger?.LogError($"The native share sheet could not be opened: {result?.Issue ?? "Unknown error"}");
            return false;
        }
        catch (HttpRequestException exception)
        {
            this.logger?.LogWarning(exception, "Failed to reach the Rust runtime share endpoint.");
            return false;
        }
        catch (TaskCanceledException exception)
        {
            this.logger?.LogWarning(exception, "Timed out while reaching the Rust runtime share endpoint.");
            return false;
        }
        catch (Exception exception)
        {
            this.logger?.LogError(exception, "Failed to process the Rust runtime share response.");
            return false;
        }
    }

    private sealed record ShareFileRequest(string FilePath);
    private sealed record ShareFileResponse(bool Success, string Issue);
}
