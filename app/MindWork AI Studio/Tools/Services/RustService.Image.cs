using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Validates and optionally optimizes a local image in the Rust runtime.
    /// </summary>
    /// <remarks>
    /// The runtime rejects files whose content does not match their extension, so the returned MIME
    /// type always describes the actual bytes.
    /// </remarks>
    /// <param name="path">The absolute path of a PNG, JPEG, or WebP image.</param>
    /// <param name="optimize">Whether the maximum-edge policy and re-encoding are applied.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The prepared image, dimensions, and stable MIME type.</returns>
    public async Task<ImagePrepareResponse> PrepareImageAsync(
        string path,
        bool optimize,
        CancellationToken token = default)
    {
        using var response = await this.http.PostAsJsonAsync("/image/prepare", new { path, optimize }, this.jsonRustSerializerOptions, token);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ImagePrepareResponse>(this.jsonRustSerializerOptions, token)
            ?? throw new InvalidDataException("The Rust image preparation returned an empty response.");
    }
}