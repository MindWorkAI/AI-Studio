namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// The mail the model asked to read, and which part of it.
/// </summary>
/// <param name="MailId">The id of the mail, in the form the index stores it.</param>
/// <param name="AttachmentNumber">The number of the attachment to read, starting at 1, or null to read the text of the mail.</param>
/// <param name="IncludeHeaders">Whether to show the complete header block as well.</param>
/// <param name="Page">The page of the text, starting at 1.</param>
internal sealed record ReadMailRequest(string MailId, int? AttachmentNumber, bool IncludeHeaders, int Page);