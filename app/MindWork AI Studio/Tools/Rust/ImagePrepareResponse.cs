namespace AIStudio.Tools.Rust;

/// <summary>
/// Contains a locally prepared image.
/// </summary>
/// <param name="DataUrl">The prepared image as a Data URL.</param>
/// <param name="MimeType">The preserved supported image MIME type.</param>
/// <param name="Width">The prepared pixel width.</param>
/// <param name="Height">The prepared pixel height.</param>
/// <param name="WasResized">Whether the maximum-edge policy resized the image.</param>
public sealed record ImagePrepareResponse(
    string DataUrl,
    string MimeType,
    uint Width,
    uint Height,
    bool WasResized);