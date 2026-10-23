namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// A picture, ready to be placed on a slide.
/// </summary>
/// <param name="Data">The encoded image.</param>
/// <param name="ContentType">The MIME type of the encoded image, e.g., "image/png".</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
internal sealed record SlidePicture(byte[] Data, string ContentType, int Width, int Height);