using AIStudio.Tools.Rust;

namespace AIStudio.Chat;

/// <summary>
/// Represents an immutable file attachment with details about its type, name, path, and size.
/// </summary>
public readonly record struct FileAttachment(FileAttachmentType Type, string FileName, string FilePath, long FileSizeBytes)
{
    /// <summary>
    /// Gets a value indicating whether the file still exists on the file system.`
    /// </summary>
    public bool Exists => File.Exists(this.FilePath);

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

        return new FileAttachment(type, fileName, filePath, fileSize);
    }

    /// <summary>
    /// Determines the file attachment type based on the file extension.
    /// Uses centrally defined file type filters from <see cref="FileTypeFilter"/>.
    /// </summary>
    /// <param name="filePath">The file path to analyze.</param>
    /// <returns>The corresponding FileAttachmentType.</returns>
    private static FileAttachmentType DetermineFileType(string filePath)
    {
        var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

        // Check if it's an image file:
        if (FileTypeFilter.AllImages.FilterExtensions.Contains(extension))
            return FileAttachmentType.IMAGE;

        // Check if it's an audio file:
        if (FileTypeFilter.AllAudio.FilterExtensions.Contains(extension))
            return FileAttachmentType.AUDIO;

        // Check if it's an allowed document file (PDF, Text, or Office):
        if (FileTypeFilter.PDF.FilterExtensions.Contains(extension) ||
            FileTypeFilter.Text.FilterExtensions.Contains(extension) ||
            FileTypeFilter.AllOffice.FilterExtensions.Contains(extension))
            return FileAttachmentType.DOCUMENT;

        // All other file types are forbidden:
        return FileAttachmentType.FORBIDDEN;
    }
}