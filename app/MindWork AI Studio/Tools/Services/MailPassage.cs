namespace AIStudio.Tools.Services;

/// <summary>
/// One chunk a search of a mailbox found, by the mail it belongs to.
/// </summary>
/// <param name="MailId">The id of the mail.</param>
/// <param name="Text">The text of the chunk.</param>
internal sealed record MailPassage(string MailId, string Text);