using System.Text.Json.Serialization;

using AIStudio.Tools.Rust;

namespace AIStudio.Chat;

/// <summary>
/// Represents an immutable file attachment with details about its type, name, path, and size.
/// </summary>
/// <param name="Type">The type of the file attachment.</param>
/// <param name="FileName">The name of the file, including extension.</param>
/// <param name="FilePath">The full path to the file, including the filename and extension. A file below the data directory, such as a transcript, is stored with a path relative to it.</param>
/// <param name="FileSizeBytes">The size of the file in bytes.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FileAttachment), typeDiscriminator: "file")]
[JsonDerivedType(typeof(FileAttachmentImage), typeDiscriminator: "image")]
[JsonDerivedType(typeof(ManagedTranscriptAttachment), typeDiscriminator: "managed_transcript")]
public record FileAttachment(FileAttachmentType Type, string FileName, string FilePath, long FileSizeBytes)
{
    //
    // The properties of the parameters, declared again to be settable. A derived attachment gets the type from
    // its own constructor, and the source generation of System.Text.Json sets every init-only property while it
    // creates the object, also one missing in the JSON, which would lose that type. Keep them first and in the
    // order of the parameters, which is the order they get stored in:
    //
    public FileAttachmentType Type { get; set; } = Type;
    public string FileName { get; set; } = FileName;

    [JsonConverter(typeof(DataDirectoryRelativePathConverter))]
    public string FilePath { get; set; } = FilePath;

    public long FileSizeBytes { get; set; } = FileSizeBytes;

    /// <summary>
    /// Gets a value indicating whether the file type is forbidden and should not be attached.
    /// </summary>
    /// <remarks>
    /// The state is determined once during construction and does not change.
    /// </remarks>
    public bool IsForbidden { get; } = Type == FileAttachmentType.FORBIDDEN;

    /// <summary>
    /// Gets a value indicating whether the file type is valid and allowed to be attached.
    /// </summary>
    /// <remarks>
    /// The state is determined once during construction and does not change.
    /// </remarks>
    public bool IsValid { get; } = Type != FileAttachmentType.FORBIDDEN;

    /// <summary>
    /// Gets a value indicating whether the file type is an image.
    /// </summary>
    /// <remarks>
    /// The state is determined once during construction and does not change.
    /// </remarks>
    public bool IsImage { get; } = Type == FileAttachmentType.IMAGE;

    /// <summary>
    /// Gets the file path for loading the file from the web browser-side (Blazor).
    /// </summary>
    public string FilePathAsUrl { get; } = FileHandler.CreateFileUrl(FilePath);
    
    /// <summary>
    /// Gets a value indicating whether the file still exists on the file system.
    /// </summary>
    /// <remarks>
    /// This property checks the file system each time it is accessed. It is therefore excluded from
    /// serialization: a path on a network share which is out of reach blocks until the SMB timeout,
    /// and the settings carry such attachments in chat templates. Every store of the settings would
    /// wait that long. Reading never used the value, so older files with the field still load.
    /// </remarks>
    [JsonIgnore]
    public bool Exists => File.Exists(this.FilePath);

    /// <summary>
    /// The entry of the provenance store which holds the text this document was first sent with, or
    /// null while it was never sent, or could not be stored.
    /// </summary>
    /// <remarks>
    /// A document is read from the disk once, see ChatThread.StoreAttachedDocumentsAsync. Every later
    /// request of the chat sends the stored text, also when the file changed or is gone by now, so
    /// what the AI received stays the same and can be shown as it was. Left out of the chat file, and
    /// of chat templates, while null.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ProvenanceId { get; set; }

    /// <summary>
    /// The state of the file which the stored text was read from, see ProvenanceId, or null while
    /// there is no stored text, or when it was stored before AI Studio noted the state.
    /// </summary>
    /// <remarks>
    /// Tells whether the file changed since, for a chat which reads changed files again, see
    /// DataChat.ReadChangedAttachmentsAgain. Left out of the chat file while null.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FileVersion? Version { get; set; }

    /// <summary>
    /// Whether the file changed since its text was stored.
    /// </summary>
    /// <remarks>
    /// Reads the state of the file. A file which is gone did not change: the stored text is all
    /// there is of it. Without a noted state, nothing tells, and the stored text stays as well.
    /// </remarks>
    public bool ChangedSinceStored() => this.ProvenanceId is not null && this.Version is { } version && FileVersion.Of(this.FilePath) is { } current && current != version;

    /// <summary>
    /// The same attachment without its stored text, e.g., for a place apart from its chat, where the file is read anew.
    /// </summary>
    public FileAttachment WithoutStoredText() => this with { ProvenanceId = null, Version = null };

    /// <summary>
    /// Rebuilds the attachment from its current file path so file type detection uses the latest rules.
    /// </summary>
    /// <remarks>
    /// The entry of its text stays: it belongs to the attachment, not to the rules.
    /// </remarks>
    public virtual FileAttachment Normalize() => FromPath(this.FilePath) with { ProvenanceId = this.ProvenanceId, Version = this.Version };

    /// <summary>
    /// Makes the same attachment for its file at another place, e.g. after the chat holding the file moved.
    /// </summary>
    /// <remarks>
    /// Deliberately no with expression: that one keeps whatever was derived from the old path when
    /// the attachment was made, such as FilePathAsUrl, and the source of an image.
    /// </remarks>
    /// <param name="filePath">The path of the file now.</param>
    /// <returns>An attachment of the same kind, with the new path.</returns>
    public virtual FileAttachment WithFilePath(string filePath) => new(this.Type, this.FileName, filePath, this.FileSizeBytes) { ProvenanceId = this.ProvenanceId, Version = this.Version };

    /// <summary>
    /// Creates a FileAttachment from a file path by automatically determining the type,
    /// extracting the filename, and reading the file size.
    /// </summary>
    /// <param name="filePath">The full path to the file.</param>
    /// <returns>A FileAttachment instance with populated properties.</returns>
    public static FileAttachment FromPath(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fileSize = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        var type = DetermineFileType(filePath);

        return type switch
        {
            FileAttachmentType.DOCUMENT => new FileAttachment(type, fileName, filePath, fileSize),
            FileAttachmentType.IMAGE => new FileAttachmentImage(fileName, filePath, fileSize),
            
            _ => new FileAttachment(type, fileName, filePath, fileSize),
        };
    }

    /// <summary>
    /// Determines the file attachment type based on the file extension or, for readable text, on
    /// the content. Uses the centrally defined file type filters from FileTypes.
    /// </summary>
    /// <param name="filePath">The file path to analyze.</param>
    /// <returns>The corresponding FileAttachmentType.</returns>
    private static FileAttachmentType DetermineFileType(string filePath)
    {
        // Check if it's an executable:
        if (FileTypes.IsAllowedPath(filePath, FileTypes.EXECUTABLES))
            return FileAttachmentType.FORBIDDEN;

        // Check if it's an image file:
        if (FileTypes.IsAllowedPath(filePath, FileTypes.IMAGE))
            return FileAttachmentType.IMAGE;

        // Check if it's an audio file:
        if (FileTypes.IsAllowedPath(filePath, FileTypes.AUDIO))
            return FileAttachmentType.AUDIO;

        //
        // Check if it's an allowed document file (PDF, Text, LaTeX, or Office). Text includes
        // READABLE_TEXT, so any readable text file is accepted, whatever its name, e.g., main.tf.
        // For a file of an unknown type, this reads up to the first 8 KB:
        //
        if (FileTypes.IsAllowedPath(filePath, FileTypes.DOCUMENT))
            return FileAttachmentType.DOCUMENT;

        return FileAttachmentType.FORBIDDEN;
    }
}