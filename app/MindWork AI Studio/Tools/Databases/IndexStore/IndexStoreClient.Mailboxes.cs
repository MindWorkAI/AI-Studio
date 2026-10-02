namespace AIStudio.Tools.Databases.IndexStore;

/// <remarks>
/// Every mail is a document like any other: its row in the files, its chunks, its vectors. What
/// follows is what only a mail has, and it rests on that document. So a mail is stored after its
/// last chunk, and deleting its document takes all of this along.
///
/// A mail is addressed by its id, which is the id of its document. Folders are addressed by their
/// path, and have to be stored before the mails which lie in them.
/// </remarks>
public abstract partial class IndexStoreClient
{
    /// <summary>
    /// Reads the folders of a mailbox and how far their sync got.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The folders, ordered by their path.</returns>
    public abstract Task<IReadOnlyList<MailFolderRecord>> GetMailFoldersAsync(string dataSourceId, CancellationToken token);

    /// <summary>
    /// Stores a folder of a mailbox, or how far its sync got.
    /// </summary>
    /// <remarks>
    /// A UIDVALIDITY other than the stored one voids every UID stored for the folder, so its
    /// locations are dropped. Mails left without any location are orphaned, not deleted: the sync
    /// finds them again under their new UIDs and links them anew, without embedding them again.
    /// That is why a new UIDVALIDITY has to be stored before the first location under it, not
    /// at the end of the pass: by then, it would drop the very locations the pass just linked.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="folder">The folder, identified by its path.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task UpsertMailFolderAsync(string dataSourceId, MailFolderRecord folder, CancellationToken token);

    /// <summary>
    /// Removes a folder which is gone from the server, or no longer part of the mailbox.
    /// </summary>
    /// <remarks>
    /// Its mails are not deleted along with it, only orphaned when they lie nowhere else: they may
    /// just have moved to a folder the sync gets to later.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="folderPath">The path of the folder.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task DeleteMailFolderAsync(string dataSourceId, string folderPath, CancellationToken token);

    /// <summary>
    /// Stores what the index keeps about a mail beyond its chunks, replacing what it kept before.
    /// </summary>
    /// <remarks>
    /// Only ever after the last chunk of the mail: indexing a document deletes it first, and that
    /// takes everything stored here along. When the mail was seen before, the earlier of both
    /// first sightings stays. A UID names one mail only, so should the index still hold one of the
    /// given locations for another mail, that location moves over, and the other mail is orphaned
    /// when it lies nowhere else.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="mail">The mail, with at least one location in a stored folder.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task UpsertMailAsync(string dataSourceId, MailRecord mail, CancellationToken token);

    /// <summary>
    /// Reads everything the index keeps about a mail beyond its chunks.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="mailId">The id of the mail.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The mail, or null when the mailbox holds no such mail.</returns>
    public abstract Task<MailRecord?> GetMailAsync(string dataSourceId, string mailId, CancellationToken token);

    /// <summary>
    /// Links a mail the index already holds to one more place on the server, without embedding it again.
    /// </summary>
    /// <remarks>
    /// This is how a moved mail and a mail under a new UIDVALIDITY are taken care of. The mail stops
    /// being an orphan, and a location held for another mail moves over as with UpsertMailAsync.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="mailId">The id of the mail.</param>
    /// <param name="location">The place, in a stored folder.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when the mail was linked, false when the mailbox holds no such mail and it has to be indexed.</returns>
    public abstract Task<bool> AddMailLocationAsync(string dataSourceId, string mailId, MailLocationRecord location, CancellationToken token);

    /// <summary>
    /// Forgets places on the server where mails no longer lie.
    /// </summary>
    /// <remarks>
    /// Mails left without any location are orphaned, not deleted, cf. GetOrphanedMailsAsync.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="folderPath">The path of the folder.</param>
    /// <param name="uids">The UIDs which are gone from that folder.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task RemoveMailLocationsAsync(string dataSourceId, string folderPath, IReadOnlyCollection<long> uids, CancellationToken token);

    /// <summary>
    /// Reads which UIDs of a folder the index holds, and their flags.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="folderPath">The path of the folder.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The flags by UID, empty when the folder is not stored.</returns>
    public abstract Task<IReadOnlyDictionary<long, MailFlags>> GetMailLocationsAsync(string dataSourceId, string folderPath, CancellationToken token);

    /// <summary>
    /// Stores the flags of mails in one folder. Flags of UIDs the index does not hold are ignored.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="folderPath">The path of the folder.</param>
    /// <param name="flagsByUid">The flags by UID.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task UpdateMailFlagsAsync(string dataSourceId, string folderPath, IReadOnlyDictionary<long, MailFlags> flagsByUid, CancellationToken token);

    /// <summary>
    /// Reads the mails which lost their last location before the given point in time.
    /// </summary>
    /// <remarks>
    /// A moved mail disappears from one folder before it shows up in the other one, maybe only in
    /// the next run. Asking with the start of the current run therefore leaves out every mail
    /// which became an orphan during that run, and those get one more run to be found again.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="orphanedBefore">Only mails orphaned before this point in time.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The keys of their documents, to delete them like any other document.</returns>
    public abstract Task<IReadOnlyList<string>> GetOrphanedMailsAsync(string dataSourceId, DateTimeOffset orphanedBefore, CancellationToken token);

    /// <summary>
    /// Reads how the sync of a mailbox as a whole stands.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The state; every value is null for a mailbox which was never synced.</returns>
    public abstract Task<MailboxSyncState> GetMailboxSyncStateAsync(string dataSourceId, CancellationToken token);

    /// <summary>
    /// Holds back a sync which would remove many mails from the index at once, until the user decides.
    /// </summary>
    /// <remarks>
    /// A count other than the one held back so far voids an earlier approval: the user agreed to
    /// a number, not to whatever a later sync comes up with.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox, which has to be stored as a data source.</param>
    /// <param name="removalCount">How many mails the sync would remove from the index.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task HoldBackMailRemovalAsync(string dataSourceId, int removalCount, CancellationToken token);

    /// <summary>
    /// Records that the user agreed to the held back removal.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="removalCount">The count the user was shown and agreed to.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when exactly that removal was held back and is now approved. False when nothing or another count is held back by now, and the user has to be asked again.</returns>
    public abstract Task<bool> ApprovePendingMailRemovalAsync(string dataSourceId, int removalCount, CancellationToken token);

    /// <summary>
    /// Records a complete sync of a mailbox. A removal held back before has been dealt with by then.
    /// </summary>
    /// <param name="dataSourceId">The mailbox, which has to be stored as a data source.</param>
    /// <param name="completedUtc">When the sync ended.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task CompleteMailboxSyncAsync(string dataSourceId, DateTimeOffset completedUtc, CancellationToken token);

    /// <summary>
    /// Reads whether the server of a mailbox refused a sign-in which nobody has dealt with yet.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The refused sign-in, or null when AI Studio may sign in.</returns>
    public abstract Task<MailboxAuthFailure?> GetMailboxAuthFailureAsync(string dataSourceId, CancellationToken token);

    /// <summary>
    /// Records a sign-in the server refused. From then on, AI Studio does not sign in on its own,
    /// not even after a restart, until the failure is cleared.
    /// </summary>
    /// <remarks>
    /// Kept apart from everything else about the mailbox, so it survives a rebuild of the index,
    /// and stored for a mailbox which was never indexed as well: the very first sign-in can fail.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="failure">The refused sign-in.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task UpsertMailboxAuthFailureAsync(string dataSourceId, MailboxAuthFailure failure, CancellationToken token);

    /// <summary>
    /// Clears a refused sign-in, when the user saved a new password, asked for another try, or
    /// deleted the mailbox.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task ClearMailboxAuthFailureAsync(string dataSourceId, CancellationToken token);

    /// <summary>
    /// Lists every mailbox the index keeps something of: an index, a refused sign-in, or both.
    /// </summary>
    /// <remarks>
    /// The refused sign-ins count on their own, since they outlive the index on purpose. What is
    /// listed here for a mailbox which is no longer configured is left over, cf.
    /// DataSourceEmbeddingService.DeleteOrphanedMailboxIndexesAsync.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The ids of the mailboxes, each once.</returns>
    public abstract Task<IReadOnlyCollection<string>> GetStoredMailboxIdsAsync(CancellationToken token);
}