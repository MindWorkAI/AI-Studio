namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// How the sync of a mailbox as a whole stands.
/// </summary>
internal sealed class MailboxSyncStateEntity
{
    public string DataSourceId { get; set; } = string.Empty;

    /// <summary>
    /// When the last complete sync of the mailbox ended, or null until one did.
    /// </summary>
    public DateTimeOffset? LastSyncCompletedUtc { get; set; }

    /// <summary>
    /// How many mails a sync would remove from the index at once, held back until the user decides,
    /// or null when nothing is held back.
    /// </summary>
    /// <remarks>
    /// Nothing on the server is touched either way. But mails removed from the index have to be
    /// embedded again should they come back, which costs time and, with a cloud provider, money.
    /// </remarks>
    public int? PendingRemovalCount { get; set; }

    /// <summary>
    /// When the user agreed to the held back removal, or null while they did not.
    /// </summary>
    public DateTimeOffset? PendingRemovalApprovedUtc { get; set; }
}