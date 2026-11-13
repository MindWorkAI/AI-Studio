namespace AIStudio.Tools.Mail;

/// <summary>
/// A mailbox which the provider of a chat may not read, or which is not configured any more.
/// </summary>
/// <remarks>
/// The mail tools offer only the mailboxes a provider may read, so this happens when the settings
/// changed after a request was prepared: the user lowered the confidence of a provider, switched a
/// preview off, or deleted the mailbox. Whoever catches it refuses the call. The message is meant
/// for the log and names the mailbox by its id only.
/// </remarks>
/// <param name="mailboxId">The id of the mailbox.</param>
public sealed class MailboxNotReadableException(string mailboxId) : Exception($"The mailbox '{mailboxId}' is not configured, or the provider of the chat may not read it.")
{
    public string MailboxId => mailboxId;
}