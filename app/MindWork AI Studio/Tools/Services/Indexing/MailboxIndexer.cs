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
/// A run reads a limited number of mails, for a limited time, and leaves the rest to the next run.
/// The first sync of a large mailbox takes hours, and the other data sources wait in the same
/// queue. Nothing is removed before a run got through the whole mailbox, and a removal of a large
/// part of it waits for the user to agree, cf. MailRemovalGuard.
///
/// Signing in is guarded, because every refused attempt brings an account closer to being locked.
/// A refused sign-in is stored, and no run signs in again until the user saves a new password,
/// tests the connection, or explicitly asks for another try. That this record can be read at all
/// rests on the index store being available, which the embedding service makes sure of before
/// every run.
///
/// Starting AI Studio asks no server anything: the run at startup shows what the index holds. Logs
/// name the mailbox by its id, never by a subject, an address, a folder, an attachment or what a
/// server answered.
/// </remarks>
/// <param name="rustService">The runtime, which holds the password in the OS keyring and reads the text of attachments.</param>
/// <param name="guardService">The prompt injection filter, which every mail passes before it is embedded.</param>
/// <param name="textChunker">Cuts the text of a mail into chunks.</param>
/// <param name="logger">The logger of the embedding service, so the log reads the same whoever writes it.</param>
internal sealed partial class MailboxIndexer(RustService rustService, PromptInjectionGuardService guardService, TextChunker textChunker, ILogger logger) : IIndexedSourceIndexer
{
    /// <summary>
    /// How many mails are fetched at once, before their text is read one after the other.
    /// </summary>
    private const int SUMMARY_BATCH_SIZE = 100;

    /// <summary>
    /// How many mails one run reads at most, whether embedding them works or not.
    /// </summary>
    private const int MAX_READ_MAILS_PER_RUN = 500;

    /// <summary>
    /// How long one run takes at most, before it leaves the rest to the next one.
    /// </summary>
    private static readonly TimeSpan MAX_RUN_DURATION = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often every mailbox is synced while AI Studio runs. It goes by the clock, so a computer
    /// which slept longer than this syncs right after waking up.
    /// </summary>
    private static readonly TimeSpan SYNC_INTERVAL = TimeSpan.FromMinutes(16);

    /// <summary>
    /// How long after the tracking started the mailboxes are synced for the first time. The run at
    /// startup asks no server anything, so this is the first sync after AI Studio started.
    /// </summary>
    private static readonly TimeSpan FIRST_SYNC_DELAY = TimeSpan.FromMinutes(1);

    private readonly IntervalRunRequester syncRequester = new(SYNC_INTERVAL, FIRST_SYNC_DELAY, DataSourceEmbeddingRefreshMode.INTERVAL_CHECK, logger);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MailboxIndexer).Namespace, nameof(MailboxIndexer));

    /// <summary>
    /// One folder after the first pass of a run.
    /// </summary>
    /// <param name="Folder">The folder as the server lists it.</param>
    /// <param name="ServerState">How the folder stood on the server during the first pass.</param>
    /// <param name="StoredAtStart">The folder as the first pass stored it.</param>
    /// <param name="Plan">What is left to do in the folder.</param>
    /// <param name="IndexedUidCount">How many of its mails belong into the index.</param>
    /// <param name="StoredLocationCount">How many locations the index held in the folder before the run.</param>
    private sealed record PlannedFolder(MailServerFolder Folder, MailFolderState ServerState, MailFolderRecord StoredAtStart, MailFolderSyncPlan Plan, int IndexedUidCount, int StoredLocationCount);

    /// <summary>
    /// What a run found gone, once it got through the whole mailbox.
    /// </summary>
    /// <param name="GoneFolderPaths">The stored folders which no longer belong to the mailbox.</param>
    /// <param name="Count">How many locations would go, those of the gone folders included.</param>
    /// <param name="IndexedCount">How many locations the index held before the run.</param>
    private sealed record PlannedRemovals(IReadOnlyList<string> GoneFolderPaths, int Count, int IndexedCount);

    /// <inheritdoc />
    public bool Supports(IDataSourceBase dataSource) => dataSource is DataSourceMailbox;

    /// <inheritdoc />
    public async Task<IndexedRunOutcome> ProcessAsync(IndexedRunContext context, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        if (context.DataSource is not DataSourceMailbox mailbox)
            throw new ArgumentException("The mailbox indexer reads mailboxes only.", nameof(context));

        if (refreshMode is DataSourceEmbeddingRefreshMode.STARTUP_HASH_CHECK)
        {
            logger.LogInformation("Showing the stored index of mailbox '{MailboxId}' without asking its server, since AI Studio is starting.", mailbox.Id);
            await this.PublishStoredStateAsync(context, token);
            return IndexedRunOutcome.DONE;
        }

        //
        // Only an explicit request of the user signs in despite a refused sign-in, and only once:
        // a retry on the embeddings page, or the repair of the index.
        //
        var authFailure = await context.IndexStore.GetMailboxAuthFailureAsync(mailbox.Id, token);
        if (authFailure is not null && refreshMode is not DataSourceEmbeddingRefreshMode.MANUAL_RETRY)
        {
            logger.LogInformation("Not signing in to mailbox '{MailboxId}' because the server refused a sign-in on {FailedAtUtc:O}, and nobody has dealt with that yet.", mailbox.Id, authFailure.FailedAtUtc);
            (await this.CreateStoredStateProgressAsync(context, token)).PublishRunFailure(GetAuthFailureMessage(authFailure), DataSourceAttention.AUTH_FAILED);
            return IndexedRunOutcome.DONE;
        }

        var password = await this.ReadPasswordAsync(mailbox);
        if (password is null)
        {
            (await this.CreateStoredStateProgressAsync(context, token)).PublishRunFailure(TB("The password of the mailbox could not be read from the operating system. Please enter it again in the settings of the mailbox."));
            return IndexedRunOutcome.DONE;
        }

        await using var connector = new ImapMailboxConnector();
        try
        {
            await connector.ConnectAsync(mailbox, password, token);
        }
        catch (MailboxConnectionException e)
        {
            logger.LogWarning("Signing in to mailbox '{MailboxId}' failed: {Failure} ({ExceptionType}).", mailbox.Id, e.Failure, e.InnerException?.GetType().Name ?? "no inner exception");
            this.RecordConnectionFailure(mailbox.Id, e.Failure);
            var progress = await this.CreateStoredStateProgressAsync(context, token);

            //
            // A network which is down says nothing about the password, so only a refusal is
            // recorded. From now on, no run signs in on its own anymore, not even after a restart.
            //
            if (e.Failure is MailboxConnectionFailure.AUTHENTICATION_FAILED)
            {
                var failure = MailboxAuthFailure.FromServerAnswer(e.InnerException?.Message ?? string.Empty);
                await context.IndexStore.UpsertMailboxAuthFailureAsync(mailbox.Id, failure, token);
                progress.PublishRunFailure(GetAuthFailureMessage(failure), DataSourceAttention.AUTH_FAILED);
            }
            else
                progress.PublishRunFailure(e.Failure.GetDescription());

            return IndexedRunOutcome.DONE;
        }

        this.syncRequester.RecordServerReached(mailbox.Id);
        if (authFailure is not null)
        {
            logger.LogInformation("Signing in to mailbox '{MailboxId}' worked again, so the refused sign-in on record is cleared.", mailbox.Id);
            await context.IndexStore.ClearMailboxAuthFailureAsync(mailbox.Id, token);
        }

        return await this.SyncAsync(context, mailbox, connector, refreshMode, token);
    }

    #region Implementation of IIndexedSourceIndexer's tracking

    /// <inheritdoc />
    /// <remarks>
    /// A server reports no changes to a mailbox, so every mailbox is synced at an interval,
    /// cf. SYNC_INTERVAL, and a mailbox whose server was out of reach is tried again soon. A mailbox
    /// whose sign-in failed is left out by the embedding service, and it would not sign in anyway.
    /// </remarks>
    public void TrackChanges(IReadOnlyCollection<IIndexedDataSource> dataSources, Func<string, DataSourceEmbeddingRefreshMode, Task> requestRun) => this.syncRequester.Track(dataSources, requestRun);

    /// <inheritdoc />
    public void StopTracking(string dataSourceId) => this.syncRequester.Stop(dataSourceId);

    /// <inheritdoc />
    public void StopTrackingAll() => this.syncRequester.StopAll();

    /// <inheritdoc />
    public void Dispose() => this.syncRequester.Dispose();

    #endregion

    /// <summary>
    /// Works through the folders of a mailbox, once the connector is signed in.
    /// </summary>
    private async Task<IndexedRunOutcome> SyncAsync(IndexedRunContext context, DataSourceMailbox mailbox, ImapMailboxConnector connector, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
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
                return IndexedRunOutcome.DONE;
            }

            var syncState = await context.IndexStore.GetMailboxSyncStateAsync(mailbox.Id, token);
            var storedFolders = (await context.IndexStore.GetMailFoldersAsync(mailbox.Id, token)).ToDictionary(folder => folder.Path, StringComparer.Ordinal);
            var plannedFolders = await this.PlanFoldersAsync(context, mailbox, connector, selection, storedFolders, token);

            //
            // Counted by location: a mail which lies in two folders counts twice, as it does on the
            // server. Telling the mails apart would mean fetching every one of them.
            //
            progress = new DocumentRunProgress(context, plannedFolders.Sum(folder => folder.IndexedUidCount), 0, string.Empty, [], logger)
            {
                LastSyncUtc = syncState.LastSyncCompletedUtc,
            };

            progress.RecordUnchanged(plannedFolders.Sum(folder => folder.Plan.KeptUids.Count));
            progress.Publish();

            //
            // Whatever the prompt injection filter removes from the mails of this run is reported
            // once, when the second pass is done, rather than once per mail.
            //
            var encounteredKeys = new HashSet<string>(StringComparer.Ordinal);
            var workedThrough = true;
            await using (guardService.BeginAction())
            {
                foreach (var folder in plannedFolders)
                {
                    if (await this.SyncNewMailsAsync(context, mailbox, connector, folder, progress, encounteredKeys, runStartedUtc, token))
                        continue;

                    workedThrough = false;
                    break;
                }
            }

            var sourceHash = BuildSourceHash(plannedFolders);
            if (!workedThrough)
                return await this.PauseAsync(mailbox, progress, sourceHash, refreshMode, token);

            //
            // Every folder is through, so what is gone can be told now: a mail missing from one
            // folder had its chance to turn up in another.
            //
            var removals = await CollectRemovalsAsync(context, mailbox, plannedFolders, storedFolders.Keys, token);
            if (MailRemovalGuard.Decide(removals.IndexedCount, removals.Count, syncState) is MailRemovalDecision.HOLD_BACK)
            {
                logger.LogWarning("Syncing mailbox '{MailboxId}' would remove {RemovalCount} of {IndexedCount} location(s) from the index at once. The removal waits for the user to agree.", mailbox.Id, removals.Count, removals.IndexedCount);
                await context.IndexStore.HoldBackMailRemovalAsync(mailbox.Id, removals.Count, token);
                await progress.PauseRunAsync(sourceHash, "mailbox sync waits for a removal to be approved", token);
                progress.PublishRunFailure(GetRemovalMessage(removals.Count), DataSourceAttention.MASS_REMOVAL_PENDING, removals.Count);
                return IndexedRunOutcome.DONE;
            }

            var deletedMails = await this.ApplyRemovalsAsync(context, mailbox, plannedFolders, removals, runStartedUtc, token);
            await ForgetVanishedFailuresAsync(context, encounteredKeys, token);

            var completedUtc = DateTimeOffset.UtcNow;
            await context.IndexStore.CompleteMailboxSyncAsync(mailbox.Id, completedUtc, token);
            progress.LastSyncUtc = completedUtc;

            await progress.CompleteRunAsync(sourceHash, "mailbox sync finished", token);
            logger.LogInformation(
                "Finished syncing mailbox '{MailboxId}'. RefreshMode={RefreshMode}, Folders={FolderCount}, Embedded={EmbeddedMails}, LinkedOrUnchanged={UnchangedMails}, PermanentlySkipped={PermanentlySkippedMails}, Failed={FailedMails}, RemovedLocations={RemovedLocations}, Deleted={DeletedMails}, SourceHashPrefix={SourceHashPrefix}.",
                mailbox.Id,
                refreshMode,
                plannedFolders.Count,
                progress.IndexedDocuments,
                progress.UnchangedDocuments,
                progress.PermanentlySkippedDocuments,
                progress.FailedDocuments,
                removals.Count,
                deletedMails,
                ShortHash(sourceHash));

            return IndexedRunOutcome.DONE;
        }
        catch (MailboxConnectionException e)
        {
            //
            // Nothing was removed yet, since that happens at the very end. What this run indexed
            // stays, and the next run picks up from there.
            //
            logger.LogWarning("Syncing mailbox '{MailboxId}' stopped: {Failure} ({ExceptionType}). What was indexed so far stays.", mailbox.Id, e.Failure, e.InnerException?.GetType().Name ?? "no inner exception");
            this.RecordConnectionFailure(mailbox.Id, e.Failure);
            await context.OptimizeCollectionIfNeededAsync("mailbox sync stopped", token);
            progress ??= await this.CreateStoredStateProgressAsync(context, token);
            progress.PublishRunFailure(e.Failure.GetDescription());
            return IndexedRunOutcome.DONE;
        }
    }

    /// <summary>
    /// Tells the sync requester whether a failed connection reached the server at all.
    /// </summary>
    /// <remarks>
    /// A server out of reach is tried again soon, since a VPN tunnel is often up only a little
    /// later. A server which answered, even with a refusal, waits for the next round; settings
    /// which never let a connection start say nothing about the server.
    /// </remarks>
    /// <param name="mailboxId">The id of the mailbox.</param>
    /// <param name="failure">Why the connection failed.</param>
    private void RecordConnectionFailure(string mailboxId, MailboxConnectionFailure failure)
    {
        if (failure is MailboxConnectionFailure.NETWORK_UNAVAILABLE)
            this.syncRequester.RecordServerOutOfReach(mailboxId);
        else if (failure is not MailboxConnectionFailure.INVALID_SETTINGS)
            this.syncRequester.RecordServerReached(mailboxId);
    }

    /// <summary>
    /// Ends a run which did its share of the work before it got through the mailbox.
    /// </summary>
    /// <remarks>
    /// Only a run which got something done hands on to another one. When every mail it read
    /// failed, e.g. because the embedding provider is down, the next run would fail all the same,
    /// and the one after it, without end. Such a run ends with its failures instead, and waits for
    /// the next time the mailbox is queued.
    /// </remarks>
    private async Task<IndexedRunOutcome> PauseAsync(DataSourceMailbox mailbox, DocumentRunProgress progress, string sourceHash, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        if (progress.IndexedDocuments is 0 && progress.FailedDocuments > 0)
        {
            logger.LogWarning("Syncing mailbox '{MailboxId}' stops without carrying on, since none of the {FailedMails} mail(s) it read could be indexed.", mailbox.Id, progress.FailedDocuments);
            await progress.CompleteRunAsync(sourceHash, "mailbox sync stopped without progress", token);
            return IndexedRunOutcome.DONE;
        }

        await progress.PauseRunAsync(sourceHash, "mailbox sync paused", token);
        logger.LogInformation(
            "Syncing mailbox '{MailboxId}' carries on in another run. RefreshMode={RefreshMode}, Embedded={EmbeddedMails}, Failed={FailedMails}, Done={DoneMails}/{TotalMails}.",
            mailbox.Id,
            refreshMode,
            progress.IndexedDocuments,
            progress.FailedDocuments,
            progress.DoneDocuments,
            progress.TotalDocuments);

        return IndexedRunOutcome.MORE_TO_DO;
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

            plannedFolders.Add(new(folder, serverState, storedAtStart, plan, indexedUids.Count, storedLocations.Count));
        }

        return plannedFolders;
    }

    /// <summary>
    /// The second pass through one folder: links or indexes the mails the index does not hold there yet.
    /// </summary>
    /// <returns>True when the folder is through, false when the run did its share of the work before.</returns>
    private async Task<bool> SyncNewMailsAsync(IndexedRunContext context, DataSourceMailbox mailbox, ImapMailboxConnector connector, PlannedFolder folder, DocumentRunProgress progress, ISet<string> encounteredKeys, DateTimeOffset runStartedUtc, CancellationToken token)
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
                return true;
            }

            foreach (var uidBatch in folder.Plan.NewUids.Chunk(SUMMARY_BATCH_SIZE))
            {
                token.ThrowIfCancellationRequested();
                if (HasDoneItsShare(progress, runStartedUtc))
                    return false;

                var summaries = await connector.FetchSummariesAsync(uidBatch, token);
                foreach (var summary in summaries.OrderByDescending(summary => summary.UniqueId.Id))
                {
                    if (HasDoneItsShare(progress, runStartedUtc))
                        return false;

                    await this.SyncNewMailAsync(context, mailbox, connector, folder.Folder.FullName, summary, progress, encounteredKeys, token);
                }
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

        return true;
    }

    /// <summary>
    /// Whether a run did its share of the work, so other data sources get their turn.
    /// </summary>
    /// <remarks>
    /// Counted are the mails which were read, whether embedding them worked or not. Linking a mail
    /// the index holds already costs next to nothing, so only the time limits that.
    /// </remarks>
    private static bool HasDoneItsShare(DocumentRunProgress progress, DateTimeOffset runStartedUtc) =>
        progress.IndexedDocuments + progress.FailedDocuments >= MAX_READ_MAILS_PER_RUN || DateTimeOffset.UtcNow - runStartedUtc >= MAX_RUN_DURATION;

    /// <summary>
    /// Works out what a run which got through the whole mailbox would remove.
    /// </summary>
    /// <remarks>
    /// Counted by location, as during the run. A mail which lies in two folders and leaves both
    /// counts twice, so the user is asked rather too often than too rarely.
    /// </remarks>
    private static async Task<PlannedRemovals> CollectRemovalsAsync(IndexedRunContext context, DataSourceMailbox mailbox, IReadOnlyList<PlannedFolder> plannedFolders, IEnumerable<string> storedFolderPaths, CancellationToken token)
    {
        // Folders which the server no longer lists, or which no longer lie below the root folder:
        var plannedPaths = plannedFolders.Select(folder => folder.Folder.FullName).ToHashSet(StringComparer.Ordinal);
        var goneFolderPaths = storedFolderPaths.Where(path => !plannedPaths.Contains(path)).ToList();

        var goneFolderLocationCount = 0;
        foreach (var folderPath in goneFolderPaths)
            goneFolderLocationCount += (await context.IndexStore.GetMailLocationsAsync(mailbox.Id, folderPath, token)).Count;

        return new(
            goneFolderPaths,
            plannedFolders.Sum(folder => folder.Plan.GoneUids.Count) + goneFolderLocationCount,
            plannedFolders.Sum(folder => folder.StoredLocationCount) + goneFolderLocationCount);
    }

    /// <summary>
    /// Forgets what is gone from the server, now that every folder had its chance to link a mail which only moved.
    /// </summary>
    /// <returns>How many mails were deleted from the index, which lost their last location before this run.</returns>
    private async Task<int> ApplyRemovalsAsync(IndexedRunContext context, DataSourceMailbox mailbox, IReadOnlyList<PlannedFolder> plannedFolders, PlannedRemovals removals, DateTimeOffset runStartedUtc, CancellationToken token)
    {
        foreach (var folder in plannedFolders.Where(folder => folder.Plan.GoneUids.Count > 0))
            await context.IndexStore.RemoveMailLocationsAsync(mailbox.Id, folder.Folder.FullName, folder.Plan.GoneUids, token);

        foreach (var folderPath in removals.GoneFolderPaths)
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
    /// Tells the user interface how the mailbox stands, from the index alone.
    /// </summary>
    /// <remarks>
    /// A refused sign-in and a removal held back are stored, so they are shown again after a
    /// restart, before any server is asked.
    /// </remarks>
    private async Task PublishStoredStateAsync(IndexedRunContext context, CancellationToken token)
    {
        var dataSourceId = context.DataSource.Id;
        var progress = await this.CreateStoredStateProgressAsync(context, token);
        if (await context.IndexStore.GetMailboxAuthFailureAsync(dataSourceId, token) is { } authFailure)
        {
            progress.PublishRunFailure(GetAuthFailureMessage(authFailure), DataSourceAttention.AUTH_FAILED);
            return;
        }

        var syncState = await context.IndexStore.GetMailboxSyncStateAsync(dataSourceId, token);
        if (syncState is { PendingRemovalCount: { } pendingRemovalCount, PendingRemovalApprovedUtc: null })
        {
            progress.PublishRunFailure(GetRemovalMessage(pendingRemovalCount), DataSourceAttention.MASS_REMOVAL_PENDING, pendingRemovalCount);
            return;
        }

        progress.PublishStoredState(syncState.LastSyncCompletedUtc is not null);
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
        var progress = new DocumentRunProgress(context, locationCount + permanentFailures.Count, 0, string.Empty, [], logger)
        {
            LastSyncUtc = (await context.IndexStore.GetMailboxSyncStateAsync(dataSourceId, token)).LastSyncCompletedUtc,
        };

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

    private static string GetRemovalMessage(int removalCount) => string.Format(
        TB("This sync would remove {0} mails from the index of AI Studio at once, so it waits for you to agree. On the server, the mails stay as they are. Should they come back later, e.g. because you choose a larger period again, they have to be embedded anew, which takes time and, with a cloud provider, money."),
        removalCount.CompactCount());

    /// <summary>
    /// Hashes how the folders of a mailbox stood on the server when a run worked through them.
    /// </summary>
    /// <remarks>
    /// Nothing compares it to decide what to do, since every run asks the server anyway. It says
    /// that the index of the mailbox can be searched, cf. DocumentRunProgress.PauseRunAsync.
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