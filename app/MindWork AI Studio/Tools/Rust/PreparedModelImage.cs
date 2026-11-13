namespace AIStudio.Tools.Rust;

/// <summary>
/// An image as the Rust runtime prepared it for an AI model, or why it could not.
/// </summary>
/// <remarks>
/// The base64 and its MIME type travel side by side, so a provider which wants a data URL builds
/// one and a provider which wants them apart, such as Anthropic, never parses one.
/// </remarks>
/// <param name="ErrorCode">NONE when the image was prepared; then the other values describe it.</param>
/// <param name="Base64">The encoded image as base64, without a data URI prefix.</param>
/// <param name="MimeType">The MIME type of the encoded image: image/jpeg or image/png.</param>
/// <param name="Width">The width of the prepared image in pixels.</param>
/// <param name="Height">The height of the prepared image in pixels.</param>
/// <param name="WasResized">Whether the runtime scaled the image down.</param>
public sealed record PreparedModelImage(ModelImageErrorCode ErrorCode, string Base64, string MimeType, uint Width, uint Height, bool WasResized)
{
    /// <summary>
    /// Whether the image can go to the model.
    /// </summary>
    /// <remarks>
    /// The image itself has to be there as well: a success without one would become an empty image
    /// in the request, which providers refuse along with the whole request.
    /// </remarks>
    public bool IsPrepared => this.ErrorCode is ModelImageErrorCode.NONE && !string.IsNullOrEmpty(this.Base64);

    /// <summary>
    /// The image as a data URL, as the OpenAI-compatible APIs take it.
    /// </summary>
    /// <returns>The data URL.</returns>
    public string ToDataUrl() => $"data:{this.MimeType};base64,{this.Base64}";

    /// <summary>
    /// An image which could not be prepared.
    /// </summary>
    /// <param name="errorCode">Why it could not be prepared.</param>
    /// <returns>The failed image.</returns>
    public static PreparedModelImage Failed(ModelImageErrorCode errorCode) => new(errorCode, string.Empty, string.Empty, 0, 0, false);
}