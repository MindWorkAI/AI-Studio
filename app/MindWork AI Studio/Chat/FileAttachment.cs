namespace AIStudio.Chat;

/// <summary>
/// Represents an immutable file attachment with details about its type, name, path, and size.
/// </summary>
public readonly record struct FileAttachment(FileAttachmentType Type, string FileName, string FilePath, long FileSize);