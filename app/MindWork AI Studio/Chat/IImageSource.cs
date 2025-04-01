namespace AIStudio.Chat;

public interface IImageSource
{
    /// <summary>
    /// The type of the image source.
    /// </summary>
    /// <remarks>
    /// Is the image source a URL, a local file path, a base64 string, etc.?
    /// </remarks>
    public ContentImageSource SourceType { get; init; }

    /// <summary>
    /// The image source.
    /// </summary>
    public string Source { get; set; }
}