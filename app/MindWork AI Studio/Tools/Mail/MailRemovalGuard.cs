using AIStudio.Tools.Databases.IndexStore;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Keeps a sync from removing a large part of a mailbox from the index without asking.
/// </summary>
/// <remarks>
/// Nothing on the server is touched either way. But a mail removed from the index has to be
/// embedded again should it come back, and much of a mailbox drops out at once for reasons the user
/// may not have meant: a smaller folder or period was picked, a folder was renamed on the server,
/// or the server lists a folder as empty for a while. Embedding all of that again costs money with
/// a cloud provider and hours with a large mailbox, so the sync asks first.
///
/// A few mails are not worth a question, however large their share: a mailbox of fifty mails is
/// embedded again in no time. Nor is a small share of a large mailbox, such as the mails of one day
/// leaving the period.
/// </remarks>
public static class MailRemovalGuard
{
    /// <summary>
    /// How many mails a removal has to reach at least before anybody is asked.
    /// </summary>
    public const int MIN_REMOVAL_COUNT = 100;

    /// <summary>
    /// Which share of the indexed mails a removal has to reach at least before anybody is asked.
    /// </summary>
    public const double MIN_REMOVAL_SHARE = 0.2;

    /// <summary>
    /// Whether a removal is large enough to ask about.
    /// </summary>
    /// <param name="indexedCount">How many the index holds.</param>
    /// <param name="removalCount">How many would go.</param>
    /// <returns>True when the user has to agree first.</returns>
    public static bool IsMassRemoval(int indexedCount, int removalCount) => removalCount >= MIN_REMOVAL_COUNT && removalCount >= indexedCount * MIN_REMOVAL_SHARE;

    /// <summary>
    /// Decides whether a sync may remove what it found gone.
    /// </summary>
    /// <remarks>
    /// The user agrees to a number, not to whatever a sync comes up with: an approval counts only
    /// for the very count which was held back and shown, cf. ApprovePendingMailRemovalAsync.
    /// </remarks>
    /// <param name="indexedCount">How many the index holds.</param>
    /// <param name="removalCount">How many would go.</param>
    /// <param name="syncState">How the sync of the mailbox stands, with any removal held back before.</param>
    /// <returns>The decision.</returns>
    public static MailRemovalDecision Decide(int indexedCount, int removalCount, MailboxSyncState syncState)
    {
        if (!IsMassRemoval(indexedCount, removalCount))
            return MailRemovalDecision.PROCEED;

        return syncState is { PendingRemovalApprovedUtc: not null } && syncState.PendingRemovalCount == removalCount
            ? MailRemovalDecision.PROCEED
            : MailRemovalDecision.HOLD_BACK;
    }
}