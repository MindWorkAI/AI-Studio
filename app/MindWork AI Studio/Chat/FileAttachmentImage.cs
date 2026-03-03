namespace AIStudio.Chat;

public record FileAttachmentImage(string FileName, string FilePath, long FileSizeBytes) : FileAttachment(FileAttachmentType.IMAGE, FileName, FilePath, FileSizeBytes), IImageSource
{
    /// <summary>
    /// The type of the image source.
    /// </summary>
    /// <remarks>
    /// Is the image source a URL, a local file path, a base64 string, etc.?
    /// </remarks>
    public ContentImageSource SourceType { get; init; } = ContentImageSource.LOCAL_PATH;

    /// <summary>
    /// The image source.
    /// </summary>
    public string Source { get; set; } = FilePath;
}