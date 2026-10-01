namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// One folder of a mailbox and how far its sync got.
/// </summary>
internal sealed class MailFolderEntity
{
    public int Id { get; set; }

    public string DataSourceId { get; set; } = string.Empty;

    /// <summary>
    /// The full path of the folder, as the server names it.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The special use the server announces for the folder, e.g., SENT, or NONE, stored by name.
    /// </summary>
    public string SpecialUse { get; set; } = string.Empty;

    /// <summary>
    /// The UIDVALIDITY the stored locations of this folder belong to.
    /// </summary>
    /// <remarks>
    /// When the server reports another one, every UID stored for this folder is void.
    /// </remarks>
    public long UidValidity { get; set; }

    /// <summary>
    /// The UIDNEXT at the end of the last complete pass over the folder, or null until one completed.
    /// </summary>
    public long? UidNext { get; set; }

    /// <summary>
    /// The HIGHESTMODSEQ at the end of the last complete pass over the folder, or null until one
    /// completed, or when the server does not support CONDSTORE.
    /// </summary>
    public long? HighestModSeq { get; set; }

    /// <summary>
    /// How many mails the folder holds on the server (STATUS MESSAGES), or null when not read yet.
    /// </summary>
    public long? ServerMessageCount { get; set; }

    /// <summary>
    /// How many of them are unread (STATUS UNSEEN), or null when not read yet.
    /// </summary>
    public long? ServerUnseenCount { get; set; }

    /// <summary>
    /// When every mail of the folder which belongs into the index was in it for the first time, or
    /// null while that first pass is still running.
    /// </summary>
    public DateTimeOffset? InitialSyncCompletedUtc { get; set; }

    public List<MailLocationEntity> Locations { get; set; } = [];
}