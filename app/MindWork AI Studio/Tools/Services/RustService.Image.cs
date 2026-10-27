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

    /// <summary>
    /// Prepares a local image for an AI model in the Rust runtime.
    /// </summary>
    /// <remarks>
    /// The runtime turns the image upright, scales it down, and encodes it anew as JPEG or PNG,
    /// within the given number of bytes. An image it cannot prepare, e.g., in an unsupported format,
    /// comes back with an error code. So does a failed call to the runtime, as UNKNOWN, and never as
    /// an empty image.
    /// </remarks>
    /// <param name="path">The absolute path of the image file.</param>
    /// <param name="maxBytes">The largest number of bytes the encoded image may have, before base64.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The prepared image, or why it could not be prepared.</returns>
    public async Task<PreparedModelImage> PrepareImageForModelAsync(string path, long maxBytes, CancellationToken token = default)
    {
        try
        {
            using var response = await this.http.PostAsJsonAsync("/image/prepare-for-model", new { path, maxBytes }, this.jsonRustSerializerOptions, token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PreparedModelImage>(this.jsonRustSerializerOptions, token)
                ?? PreparedModelImage.Failed(ModelImageErrorCode.UNKNOWN);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            this.logger?.LogError(e, "Preparing the image '{ImagePath}' for an AI model failed in the runtime.", path);
            return PreparedModelImage.Failed(ModelImageErrorCode.UNKNOWN);
        }
    }
}