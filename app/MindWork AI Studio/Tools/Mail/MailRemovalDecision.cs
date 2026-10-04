namespace AIStudio.Tools.Mail;

/// <summary>
/// Whether a sync may remove the mails it found gone from the index.
/// </summary>
public enum MailRemovalDecision
{
    /// <summary>
    /// The mails go: they are few, or the user agreed to exactly this removal.
    /// </summary>
    PROCEED,

    /// <summary>
    /// The mails stay in the index until the user decides, since rebuilding them would be costly.
    /// </summary>
    HOLD_BACK,
}