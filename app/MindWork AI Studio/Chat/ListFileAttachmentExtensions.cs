namespace AIStudio.Chat;

public static class ListFileAttachmentExtensions
{
    public static bool ContainsImages(this List<FileAttachment> attachments) => attachments.Any(attachment => attachment.IsImage);

    /// <summary>
    /// Whether preparing the message for the AI may read a document through the runtime.
    /// </summary>
    /// <remarks>
    /// Every attachment but a picture counts: its type is checked again with the latest rules
    /// before it is read, cf. FileAttachment.Normalize, and may turn out to be a document then.
    /// Checking that here would touch every file twice, which takes long on a network drive.
    /// </remarks>
    /// <param name="attachments">The attachments of the message.</param>
    /// <returns>True when a document may be read.</returns>
    public static bool MayContainDocuments(this List<FileAttachment> attachments) => attachments.Any(attachment => !attachment.IsImage);
}