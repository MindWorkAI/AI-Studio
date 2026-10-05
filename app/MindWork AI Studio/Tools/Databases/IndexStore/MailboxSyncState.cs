namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// How the sync of a mailbox as a whole stands.
/// </summary>
/// <param name="LastSyncCompletedUtc">When the last complete sync ended, or null until one did.</param>
/// <param name="PendingRemovalCount">How many mails a sync would remove from the index at once, held back until the user decides, or null when nothing is held back.</param>
/// <param name="PendingRemovalApprovedUtc">When the user agreed to exactly that removal, or null while they did not.</param>
public sealed record MailboxSyncState(DateTimeOffset? LastSyncCompletedUtc, int? PendingRemovalCount, DateTimeOffset? PendingRemovalApprovedUtc);