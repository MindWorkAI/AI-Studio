using System.Text.Json.Serialization;

namespace AIStudio.Chat;

public record FileAttachmentImage(string FileName, string FilePath, long FileSizeBytes) : FileAttachment(FileAttachmentType.IMAGE, FileName, FilePath, FileSizeBytes), IImageSource
{
    /// <summary>
    /// The type of the image source.
    /// </summary>
    /// <remarks>
    /// Is the image source a URL, a local file path, a base64 string, etc.?
    /// </remarks>
    public ContentImageSource SourceType { get; set; } = ContentImageSource.LOCAL_PATH;

    /// <summary>
    /// The image source.
    /// </summary>
    /// <remarks>
    /// An attached image is a file, so its source is the path of that file. It is not stored a second
    /// time: the stored file path is relative when the image lies below the data directory, and the
    /// source has to follow it when the chat is read again, on whatever computer that happens.
    /// </remarks>
    [JsonIgnore]
    public string Source { get; set; } = FilePath;

    /// <inheritdoc />
    public override FileAttachment WithFilePath(string filePath) => new FileAttachmentImage(this.FileName, filePath, this.FileSizeBytes)
    {
        SourceType = this.SourceType,
        Source = this.SourceType is ContentImageSource.LOCAL_PATH ? filePath : this.Source,
        ProvenanceId = this.ProvenanceId,
        Version = this.Version,
    };
}