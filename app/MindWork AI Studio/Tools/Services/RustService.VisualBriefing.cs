using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Validates and optionally optimizes a local visual asset in the Rust runtime.
    /// </summary>
    /// <param name="path">The absolute path of a PNG, JPEG, or WebP image.</param>
    /// <param name="optimize">Whether the visual-briefing optimization policy is enabled.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The prepared image, dimensions, and stable MIME type.</returns>
    public async Task<VisualBriefingImageResponse> PrepareVisualBriefingImageAsync(
        string path,
        bool optimize,
        CancellationToken token = default)
    {
        using var response = await this.http.PostAsJsonAsync("/visual-briefing/image", new { path, optimize }, this.jsonRustSerializerOptions, token);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<VisualBriefingImageResponse>(this.jsonRustSerializerOptions, token)
            ?? throw new InvalidDataException("The Rust image optimizer returned an empty response.");
    }
}