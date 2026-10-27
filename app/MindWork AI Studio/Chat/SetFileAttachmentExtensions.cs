namespace AIStudio.Chat;

public static class SetFileAttachmentExtensions
{
    /// <summary>
    /// Attaches a file in place of the attachment of the same file the set held before.
    /// </summary>
    /// <remarks>
    /// The attachments differ by more than their files: one of a message being edited holds the
    /// entry of the text it was sent with, and the size of the file may have changed since. Both
    /// would stay in the set, and the message would go with the old and the new text of the file.
    /// Attaching a file again means the file as it is now.
    /// </remarks>
    /// <param name="attachments">The attachments of a message.</param>
    /// <param name="attachment">The attachment of the file just attached.</param>
    public static void AttachInPlaceOfSameFile(this HashSet<FileAttachment> attachments, FileAttachment attachment)
    {
        attachments.RemoveWhere(existing => string.Equals(existing.FilePath, attachment.FilePath, StringComparison.Ordinal));
        attachments.Add(attachment);
    }
}