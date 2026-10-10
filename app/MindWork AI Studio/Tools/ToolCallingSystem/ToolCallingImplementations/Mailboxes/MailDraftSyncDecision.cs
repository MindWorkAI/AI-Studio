namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// What a mail tool does about drafts which may have changed since the last sync of a mailbox.
/// </summary>
public enum MailDraftSyncDecision
{
    /// <summary>
    /// Nothing: the tool does not ask for drafts, the last sync is recent, or the mailbox waits for the user to sign in again.
    /// </summary>
    NOT_NEEDED,

    /// <summary>
    /// A sync of the mailbox is due, and the tool requests it. The result says that the drafts may be outdated until it is done.
    /// </summary>
    SYNC_REQUESTED,

    /// <summary>
    /// A sync would be due, but the user switched off that the data sources refresh on their own. The result says how to sync by hand.
    /// </summary>
    AUTOMATIC_REFRESH_OFF,
}