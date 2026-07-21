using AIStudio.Chat;

namespace AIStudio.Assistants.Dynamic;

public sealed class FileAttachmentState
{
    public HashSet<FileAttachment> DocumentPaths { get; set; } = [];
}
