using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Security;

using static AIStudio.Tools.Services.Indexing.IndexingLogFormat;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Indexes e-mail mailboxes on IMAP servers.
/// </summary>
/// <remarks>
/// A mail is a document under its content key, cf. MailContentKey, the same in every folder it lies
/// in. Where it lies is kept apart from it, as its locations: a mail which moves, or which the server
/// numbers anew, only changes its locations and is never embedded again.
///
/// A run signs in once and goes through the folders twice. The first pass asks every folder which
/// mails belong into the index and settles all which needs no text: the flags, and what is gone.
/// The second pass fetches the new mails, the newest first, links those the index holds already,
/// and reads and embeds the others. What is gone is removed only at the very end, after every folder
/// had its chance to link a mail which merely moved there. Even then, a mail is only orphaned, and
/// deleted one run later, should it not turn up again by then.
///
/// Signing in is guarded, because every refused attempt brings an account closer to being locked.
/// A refused sign-in is stored, and no run signs in again until the user saves a new password,
/// tests the connection, or explicitly asks for another try. That this record can be read at all
/// rests on the index store being available, which the embedding service makes sure of before
/// every run.
///
/// Starting AI Studio asks no server anything: the run at startup shows what the index holds. Logs
/// name the mailbox by its id, never by a subject, an address, a folder or what a server answered.
/// </remarks>
/// <param name="rustService">The runtime, which holds the password in the OS keyring.</param>
/// <param name="guardService">The prompt injection filter, which every mail passes before it is embedded.</param>
/// <param name="textChunker">Cuts the text of a mail into chunks.</param>
/// <param name="logger">The logger of the embedding service, so the log reads the same whoever writes it.</param>
internal sealed partial class MailboxIndexer(RustService rustService, PromptInjectionGuardService guardService, TextChunker textChunker, ILogger logger) : IIndexedSourceIndexer
{
    /// <summary>
    /// How many mails are fetched at once, before their text is read one after the other.
    /// </summary>
    private const int SUMMARY_BATCH_SIZE = 100;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MailboxIndexer).Namespace, nameof(MailboxIndexer));

    /// <summary>
    /// One folder after the first pass of a run.
    /// </summary>
    /// <param name="Folder">The folder as the server lists it.</param>
    /// <param name="ServerState">How the folder stood on the server during the first pass.</param>
    /// <param name="StoredAtStart">The folder as the first pass stored it.</param>
    /// <param name="Plan">What is left to do in the folder.</param>
    /// <param name="IndexedUidCount">How many of its mails belong into the index.</param>
    private sealed record PlannedFolder(MailServerFolder Folder, MailFolderState ServerState, MailFolderRecord StoredAtStart, MailFolderSyncPlan Plan, int IndexedUidCount);

    /// <inheritdoc />
    public bool Supports(IDataSourceBase dataSource) => dataSource is DataSourceMailbox;

    /// <inheritdoc />
    public async Task ProcessAsync(IndexedRunContext context, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        if (context.DataSource is not DataSourceMailbox mailbox)
            throw new ArgumentException("The mailbox indexer reads mailboxes only.", nameof(context));

        if (refreshMode is DataSourceEmbeddingRefreshMode.STARTUP_HASH_CHECK)
        {
            logger.LogInformation("Showing the stored index of mailbox '{MailboxId}' without asking its server, since AI Studio is starting.", mailbox.Id);
            (await this.CreateStoredStateProgressAsync(context, token)).PublishStoredState();
            return;
        }

        //
        // Only an explicit request of the user signs in despite a refused sign-in, and only once:
        // a retry on the embeddings page, or the repair of the index.
        //
        var authFailure = await context.IndexStore.GetMailboxAuthFailureAsync(mailbox.Id, token);
        if (authFailure is not null && refreshMode is not DataSourceEmbeddingRefreshMode.MANUAL_RETRY)
        {
            logger.LogInformation("Not signing in to mailbox '{MailboxId}' because the server refused a sign-in on {FailedAtUtc:O}, and nobody has dealt with that yet.", mailbox.Id, authFailure.FailedAtUtc);
            (await this.CreateStoredStateProgressAsync(context, token)).PublishRunFailure(GetAuthFailureMessage(authFailure));
            return;
        }

        var password = await this.ReadPasswordAsync(mailbox);
        if (password is null)
        {
            (await this.CreateStoredStateProgressAsync(context, token)).PublishRunFailure(TB("The password of the mailbox could not be read from the operating system. Please enter it again in the settings of the mailbox."));
            return;
        }

        await using var connector = new ImapMailboxConnector();
        try
        {
            await connector.ConnectAsync(mailbox, password, token);
        }
        catch (MailboxConnectionException e)
        {
            logger.LogWarning("Signing in to mailbox '{MailboxId}' failed: {Failure} ({ExceptionType}).", mailbox.Id, e.Failure, e.InnerException?.GetType().Name ?? "no inner exception");
            var message = e.Failure.GetDescription();

            //
            // A network which is down says nothing about the password, so only a refusal is
            // recorded. From now on, no run signs in on its own anymore, not even after a restart.
            //
            if (e.Failure is MailboxConnectionFailure.AUTHENTICATION_FAILED)
            {
                var failure = MailboxAuthFailure.FromServerAnswer(e.InnerException?.Message ?? string.Empty);
                await context.IndexStore.UpsertMailboxAuthFailureAsync(mailbox.Id, failure, token);
                message = GetAuthFailureMessage(failure);
            }

            (await this.CreateStoredStateProgressAsync(context, token)).PublishRunFailure(message);
            return;
        }

        if (authFailure is not null)
        {
            logger.LogInformation("Signing in to mailbox '{MailboxId}' worked again, so the refused sign-in on record is cleared.", mailbox.Id);
            await context.IndexStore.ClearMailboxAuthFailureAsync(mailbox.Id, token);
        }

        await this.SyncAsync(context, mailbox, connector, refreshMode, token);
    }

    #region Implementation of IIndexedSourceIndexer's tracking

    /// <inheritdoc />
    /// <remarks>
    /// Mailboxes are not watched: a mailbox is synced whenever it is queued.
    /// </remarks>
    public void TrackChanges(IReadOnlyCollection<IIndexedDataSource> dataSources, Func<string, DataSourceEmbeddingRefreshMode, Task> requestRun)
    {
    }

    /// <inheritdoc />
    public void StopTracking(string dataSourceId)
    {
    }

    /// <inheritdoc />
    public void StopTrackingAll()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    #endregion

    /// <summary>
    /// Works through the folders of a mailbox, once the connector is signed in.
    /// </summary>
    private async Task SyncAsync(IndexedRunContext context, DataSourceMailbox mailbox, ImapMailboxConnector connector, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        var runStartedUtc = DateTimeOffset.UtcNow;
        DocumentRunProgress? progress = null;

        try
        {
            var selection = MailFolderSelection.Select(await connector.GetFoldersAsync(token), mailbox.RootFolder);
            if (!selection.RootFolderFound)
            {
                //
                // Never the same as an empty mailbox. Read that way, renaming a folder on the server
                // would remove every mail from the index, and embedding them again costs hours.
                //
                logger.LogWarning("The server no longer lists the root folder of mailbox '{MailboxId}'. The sync stops, and nothing is removed from the index.", mailbox.Id);
                (await this.CreateStoredStateProgressAsync(context, token)).PublishRunFailure(string.Format(TB("The server no longer lists the folder '{0}' to which this mailbox is limited. Perhaps it was renamed or deleted. Nothing was removed from the index. Rename the folder back on the server, or add the mailbox anew."), mailbox.RootFolder));
                return;
            }

            var storedFolders = (await context.IndexStore.GetMailFoldersAsync(mailbox.Id, token)).ToDictionary(folder => folder.Path, StringComparer.Ordinal);
            var plannedFolders = await this.PlanFoldersAsync(context, mailbox, connector, selection, storedFolders, token);

            //
            // Counted by location: a mail which lies in two folders counts twice, as it does on the
            // server. Telling the mails apart would mean fetching every one of them.
            //
            progress = new DocumentRunProgress(context, plannedFolders.Sum(folder => folder.IndexedUidCount), 0, string.Empty, [], logger);
            progress.RecordUnchanged(plannedFolders.Sum(folder => folder.Plan.KeptUids.Count));
            progress.Publish();

            //
            // Whatever the prompt injection filter removes from the mails of this run is reported
            // once, when the second pass is done, rather than once per mail.
            //
            var encounteredKeys = new HashSet<string>(StringComparer.Ordinal);
            await using (guardService.BeginAction())
            {
                foreach (var folder in plannedFolders)
                    await this.SyncNewMailsAsync(context, mailbox, connector, folder, progress, encounteredKeys, token);
            }

            var deletedMails = await this.ApplyRemovalsAsync(context, mailbox, plannedFolders, storedFolders.Keys, runStartedUtc, token);
            await ForgetVanishedFailuresAsync(context, encounteredKeys, token);
            await context.IndexStore.CompleteMailboxSyncAsync(mailbox.Id, DateTimeOffset.UtcNow, token);

            var sourceHash = BuildSourceHash(plannedFolders);
            await progress.CompleteRunAsync(sourceHash, "mailbox sync finished", token);
            logger.LogInformation(
                "Finished syncing mailbox '{MailboxId}'. RefreshMode={RefreshMode}, Folders={FolderCount}, Embedded={EmbeddedMails}, LinkedOrUnchanged={UnchangedMails}, PermanentlySkipped={PermanentlySkippedMails}, Failed={FailedMails}, Deleted={DeletedMails}, SourceHashPrefix={SourceHashPrefix}.",
                mailbox.Id,
                refreshMode,
                plannedFolders.Count,
                progress.IndexedDocuments,
                progress.UnchangedDocuments,
                progress.PermanentlySkippedDocuments,
                progress.FailedDocuments,
                deletedMails,
                ShortHash(sourceHash));
        }
        catch (MailboxConnectionException e)
        {
            //
            // Nothing was removed yet, since that happens at the very end. What this run indexed
            // stays, and the next run picks up from there.
            //
            logger.LogWarning("Syncing mailbox '{MailboxId}' stopped: {Failure} ({ExceptionType}). What was indexed so far stays.", mailbox.Id, e.Failure, e.InnerException?.GetType().Name ?? "no inner exception");
            await context.OptimizeCollectionIfNeededAsync("mailbox sync stopped", token);
            progress ??= await this.CreateStoredStateProgressAsync(context, token);
            progress.PublishRunFailure(e.Failure.GetDescription());
        }
    }

    /// <summary>
    /// The first pass: finds out what there is to do in every folder, and settles all of it which needs no text.
    /// </summary>
    private async Task<IReadOnlyList<PlannedFolder>> PlanFoldersAsync(IndexedRunContext context, DataSourceMailbox mailbox, ImapMailboxConnector connector, MailFolderSelection selection, IReadOnlyDictionary<string, MailFolderRecord> storedFolders, CancellationToken token)
    {
        var receivedSince = mailbox.MaxAge.GetReceivedSince(DateTimeOffset.UtcNow);
        var plannedFolders = new List<PlannedFolder>(selection.Folders.Count);
        foreach (var folder in selection.Folders)
        {
            token.ThrowIfCancellationRequested();

            var serverState = await connector.OpenFolderAsync(folder.FullName, token);
            var storedFolder = storedFolders.GetValueOrDefault(folder.FullName);
            var storedLocations = await context.IndexStore.GetMailLocationsAsync(mailbox.Id, folder.FullName, token);
            var indexedUids = await connector.SearchIndexedMailsAsync(receivedSince, token);
            var plan = MailFolderSyncPlan.Create(storedFolder, storedLocations, serverState, indexedUids);

            //
            // Stored before anything else happens in the folder: a new UIDVALIDITY drops the stored
            // locations, and it has to do so before the first location under it is linked. Where
            // the last complete pass got to stays until this pass is complete as well.
            //
            var storedAtStart = new MailFolderRecord(
                folder.FullName,
                folder.SpecialUse,
                serverState.UidValidity,
                plan.UidValidityChanged ? null : storedFolder?.UidNext,
                plan.UidValidityChanged ? null : storedFolder?.HighestModSeq,
                serverState.MessageCount,
                serverState.UnseenCount,
                storedFolder?.InitialSyncCompletedUtc);

            await context.IndexStore.UpsertMailFolderAsync(mailbox.Id, storedAtStart, token);
            if (plan.UidValidityChanged)
                logger.LogInformation("The server numbered folder {FolderNumber} of mailbox '{MailboxId}' anew. Its mails are linked again by their keys, without embedding them again.", plannedFolders.Count + 1, mailbox.Id);

            if (plan.ChecksFlags)
            {
                var fetchedFlags = await connector.FetchFlagsAsync(plan.KeptUids, plan.FlagsChangedSinceModSeq, token);
                await context.IndexStore.UpdateMailFlagsAsync(mailbox.Id, folder.FullName, MailFolderSyncPlan.GetChangedFlags(storedLocations, fetchedFlags), token);
            }

            logger.LogDebug(
                "Planned folder {FolderNumber}/{FolderCount} of mailbox '{MailboxId}'. New={NewMails}, Gone={GoneMails}, Kept={KeptMails}, ChecksFlags={ChecksFlags}.",
                plannedFolders.Count + 1,
                selection.Folders.Count,
                mailbox.Id,
                plan.NewUids.Count,
                plan.GoneUids.Count,
                plan.KeptUids.Count,
                plan.ChecksFlags);

            plannedFolders.Add(new(folder, serverState, storedAtStart, plan, indexedUids.Count));
        }

        return plannedFolders;
    }

    /// <summary>
    /// The second pass through one folder: links or indexes the mails the index does not hold there yet.
    /// </summary>
    private async Task SyncNewMailsAsync(IndexedRunContext context, DataSourceMailbox mailbox, ImapMailboxConnector connector, PlannedFolder folder, DocumentRunProgress progress, ISet<string> encounteredKeys, CancellationToken token)
    {
        if (folder.Plan.NewUids.Count > 0)
        {
            //
            // The server may have numbered the folder anew since the first pass. Its UIDs would
            // name other mails then, so the folder waits for the next run, which notices it.
            //
            var serverState = await connector.OpenFolderAsync(folder.Folder.FullName, token);
            if (serverState.UidValidity != folder.ServerState.UidValidity)
            {
                logger.LogInformation("The server numbered a folder of mailbox '{MailboxId}' anew during the sync. The folder is synced during the next run.", mailbox.Id);
                return;
            }

            foreach (var uidBatch in folder.Plan.NewUids.Chunk(SUMMARY_BATCH_SIZE))
            {
                token.ThrowIfCancellationRequested();

                var summaries = await connector.FetchSummariesAsync(uidBatch, token);
                foreach (var summary in summaries.OrderByDescending(summary => summary.UniqueId.Id))
                    await this.SyncNewMailAsync(context, mailbox, connector, folder.Folder.FullName, summary, progress, encounteredKeys, token);
            }
        }

        //
        // The folder is through. From now on, only what changed after the first pass of this run
        // needs to be asked about.
        //
        await context.IndexStore.UpsertMailFolderAsync(mailbox.Id, folder.StoredAtStart with
        {
            UidNext = folder.ServerState.UidNext,
            HighestModSeq = folder.ServerState.HighestModSeq,
            InitialSyncCompletedUtc = folder.StoredAtStart.InitialSyncCompletedUtc ?? DateTimeOffset.UtcNow,
        }, token);
    }

    /// <summary>
    /// Forgets what is gone from the server, now that every folder had its chance to link a mail which only moved.
    /// </summary>
    /// <returns>How many mails were deleted from the index, which lost their last location before this run.</returns>
    private async Task<int> ApplyRemovalsAsync(IndexedRunContext context, DataSourceMailbox mailbox, IReadOnlyList<PlannedFolder> plannedFolders, IEnumerable<string> storedFolderPaths, DateTimeOffset runStartedUtc, CancellationToken token)
    {
        foreach (var folder in plannedFolders.Where(folder => folder.Plan.GoneUids.Count > 0))
            await context.IndexStore.RemoveMailLocationsAsync(mailbox.Id, folder.Folder.FullName, folder.Plan.GoneUids, token);

        // A folder which the server no longer lists, or which no longer lies below the root folder:
        var plannedPaths = plannedFolders.Select(folder => folder.Folder.FullName).ToHashSet(StringComparer.Ordinal);
        foreach (var folderPath in storedFolderPaths.Where(path => !plannedPaths.Contains(path)).ToList())
            await context.IndexStore.DeleteMailFolderAsync(mailbox.Id, folderPath, token);

        //
        // A mail which lost its last location during this run gets one more run to turn up again,
        // e.g. when it was moved into a folder this run had already been through:
        //
        var orphanedKeys = await context.IndexStore.GetOrphanedMailsAsync(mailbox.Id, runStartedUtc, token);
        foreach (var key in orphanedKeys)
        {
            token.ThrowIfCancellationRequested();
            await context.DeleteDocumentPointsAsync(key, token);
            await context.IndexStore.DeleteFileAsync(mailbox.Id, key, token);
            context.Manifest.Files.Remove(key);
        }

        return orphanedKeys.Count;
    }

    /// <summary>
    /// Forgets the marks of mails skipped for good which did not turn up during this run.
    /// </summary>
    /// <remarks>
    /// Such a mail never gets a location, so every run comes across it among the new mails, for as
    /// long as it belongs into the index. One which did not turn up is gone, and its mark would stay
    /// forever.
    /// </remarks>
    private static async Task ForgetVanishedFailuresAsync(IndexedRunContext context, IReadOnlySet<string> encounteredKeys, CancellationToken token)
    {
        foreach (var key in context.Manifest.PermanentFailures.Keys.Where(key => !encounteredKeys.Contains(key)).ToList())
            await context.ForgetPermanentFailureAsync(key, token);
    }

    /// <summary>
    /// Counts what the index holds of a mailbox, for a status which comes without a run.
    /// </summary>
    /// <remarks>
    /// Counted by location, as during a run, cf. SyncAsync.
    /// </remarks>
    private async Task<DocumentRunProgress> CreateStoredStateProgressAsync(IndexedRunContext context, CancellationToken token)
    {
        var dataSourceId = context.DataSource.Id;
        var locationCount = 0;
        foreach (var folder in await context.IndexStore.GetMailFoldersAsync(dataSourceId, token))
            locationCount += (await context.IndexStore.GetMailLocationsAsync(dataSourceId, folder.Path, token)).Count;

        var permanentFailures = context.Manifest.PermanentFailures;
        var progress = new DocumentRunProgress(context, locationCount + permanentFailures.Count, 0, string.Empty, [], logger);
        progress.RecordUnchanged(locationCount);
        foreach (var (key, failure) in permanentFailures)
            progress.RecordStillUnreadable(key, failure);

        return progress;
    }

    private async Task<string?> ReadPasswordAsync(DataSourceMailbox mailbox)
    {
        var requestedSecret = await rustService.GetSecret(mailbox, SecretStoreType.DATA_SOURCE, isTrying: true);
        if (requestedSecret.Success)
            return await requestedSecret.Secret.Decrypt(Program.ENCRYPTION);

        logger.LogWarning("Could not read the password of mailbox '{MailboxId}' from the OS keyring.", mailbox.Id);
        return null;
    }

    private static string GetAuthFailureMessage(MailboxAuthFailure failure) => string.Format(
        TB("Signing in to the mailbox failed on {0}. Presumably your password changed. AI Studio does not try again on its own, so that your account is not locked. Please enter your current password in the settings of the mailbox."),
        failure.FailedAtUtc.ToLocalTime().ToString("g", I18N.I.Culture));

    /// <summary>
    /// Hashes how the folders of a mailbox stood on the server when a run worked through them.
    /// </summary>
    /// <remarks>
    /// Nothing compares it to decide what to do, since every run asks the server anyway. It says
    /// that a run got through the whole mailbox, cf. DocumentRunProgress.CompleteRunAsync.
    /// </remarks>
    private static string BuildSourceHash(IReadOnlyList<PlannedFolder> plannedFolders)
    {
        var source = new StringBuilder("mailbox");
        foreach (var folder in plannedFolders)
        {
            var state = folder.ServerState;
            source.Append('\n').Append(string.Create(CultureInfo.InvariantCulture, $"{folder.Folder.FullName}|{state.UidValidity}|{state.UidNext}|{state.HighestModSeq}|{state.MessageCount}"));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString())));
    }
}