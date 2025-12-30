namespace AIStudio.Chat;

public static class ListFileAttachmentExtensions
{
    public static bool ContainsImages(this List<FileAttachment> attachments) => attachments.Any(attachment => attachment.IsImage);
}