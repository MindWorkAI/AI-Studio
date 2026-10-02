using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services.Indexing;

using static AIStudio.Tools.Services.Indexing.IndexingLogFormat;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService(SettingsManager settingsManager, RustService rustService, DatabaseClientProvider databaseClientProvider,
    PromptInjectionGuardService guardService, ILogger<DataSourceEmbeddingService> logger) : BackgroundService
{
    /// <summary>
    /// How long the re-index check waits for the index database before it gives up.
    /// </summary>
    /// <remarks>
    /// Asked while somebody waits for the data source selection to open, and possibly while a run
    /// writes to the same database.
    /// </remarks>
    private static readonly TimeSpan REINDEX_CHECK_TIMEOUT = TimeSpan.FromSeconds(2);

    /// <summary>
    /// One indexer per kind of data source this service indexes.
    /// </summary>
    private readonly IReadOnlyList<IIndexedSourceIndexer> indexers = CreateIndexers(settingsManager, rustService, guardService, logger);

    private readonly Channel<DataSourceEmbeddingQueueItem> queue = Channel.CreateUnbounded<DataSourceEmbeddingQueueItem>();
    private readonly ConcurrentDictionary<string, byte> queuedIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> runningIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DataSourceEmbeddingRefreshMode> pendingRefreshModes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DataSourceRunControl> activeRuns = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DataSourceEmbeddingStatus> statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly object queueStateLock = new();
    private int startupHashCheckStarted;
    private int startupHashCheckCompleted;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceEmbeddingService).Namespace, nameof(DataSourceEmbeddingService));

    private enum DataSourceQueueRequestResult
    {
        QUEUED,
        ALREADY_QUEUED,
        RUNNING,
        RUNNING_MARKED_PENDING,
    }

    private sealed record DataSourceEmbeddingQueueItem(string DataSourceId, DataSourceEmbeddingRefreshMode RefreshMode);

    private sealed record DataSourceRunControl(CancellationTokenSource TokenSource, TaskCompletionSource<object?> Completion);

    /// <summary>
    /// Creates one indexer per kind of data source, all of them cutting their text with the same chunker.
    /// </summary>
    private static IReadOnlyList<IIndexedSourceIndexer> CreateIndexers(SettingsManager settingsManager, RustService rustService, PromptInjectionGuardService guardService, ILogger logger)
    {
        var textChunker = new TextChunker(rustService, logger);
        return
        [
            new FileSourceIndexer(settingsManager, rustService, guardService, textChunker, logger),
            new MailboxIndexer(rustService, guardService, textChunker, logger),
        ];
    }

    public IReadOnlyList<DataSourceEmbeddingStatus> GetStatuses()
    {
        return this.statuses.Values
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.DataSourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public DataSourceEmbeddingOverview GetOverview()
    {
        var orderedStatuses = this.GetStatuses();
        var activeStatus = orderedStatuses
            .FirstOrDefault(status => status.State is DataSourceEmbeddingState.QUEUED or DataSourceEmbeddingState.RUNNING);

        if (activeStatus is not null)
        {
            var total = Math.Max(activeStatus.TotalDocuments, 1);
            return new(
                activeStatus.State,
                activeStatus.IndexedDocuments,
                total,
                activeStatus.FailedDocuments);
        }

        var failedStatus = orderedStatuses
            .FirstOrDefault(status => status.State is DataSourceEmbeddingState.FAILED || status.FailedDocuments > 0);

        if (failedStatus is not null)
            return new(DataSourceEmbeddingState.FAILED, failedStatus.IndexedDocuments, failedStatus.TotalDocuments, failedStatus.FailedDocuments);

        return new(DataSourceEmbeddingState.COMPLETED, 0, 0, 0);
    }

    public Task QueueAllInternalDataSourcesAsync()
    {
        return this.QueueAllInternalDataSourcesAsync(true);
    }

    private Task QueueAllInternalDataSourcesAsync(bool queueAfterCurrentRun)
    {
        this.RefreshWatchers();

        var supportedDataSources = this.GetConfiguredIndexedSources();

        logger.LogInformation(
            "Queueing {DataSourceCount} supported internal data source(s) for background embedding hash checks. QueueAfterCurrentRun={QueueAfterCurrentRun}.",
            supportedDataSources.Count,
            queueAfterCurrentRun);

        var tasks = supportedDataSources.Select(dataSource => this.QueueDataSourceAsync(dataSource, queueAfterCurrentRun, DataSourceEmbeddingRefreshMode.HASH_CHECK));

        return Task.WhenAll(tasks);
    }

    public Task QueueAllInternalDataSourcesIfAutomaticRefreshAsync()
    {
        if (!settingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh)
        {
            this.RefreshWatchers();
            return Task.CompletedTask;
        }

        logger.LogDebug("Automatic startup embedding hash check is handled by the background service. Ignoring duplicate startup queue request.");
        return Task.CompletedTask;
    }

    public void RefreshAutomaticWatchers()
    {
        if (!settingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh)
        {
            Volatile.Write(ref this.startupHashCheckCompleted, 0);
            Interlocked.Exchange(ref this.startupHashCheckStarted, 0);
            this.RemoveAllWatchers();
            return;
        }

        if (Volatile.Read(ref this.startupHashCheckCompleted) == 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await this.RunInitialDataSourceHashCheckAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Failed to run the initial data source hash check after automatic refresh was enabled.");
                }
            });
            return;
        }

        this.RefreshWatchers();
    }

    public bool CanRefreshDataSource(IDataSourceBase dataSource)
    {
        return this.IsSupportedIndexedSource(dataSource);
    }

    public bool CanRefreshDataSource(string dataSourceId)
    {
        return this.TryGetConfiguredIndexedSource(dataSourceId, out _);
    }

    /// <summary>
    /// Whether the file or folder a data source reads must stay as it is.
    /// </summary>
    /// <remarks>
    /// Locked as soon as the index holds anything, because where a data source reads from is what it
    /// is: another folder is another data source, and the path reaches no signature, so swapping it
    /// would leave the stored index describing documents nobody points at any more.
    ///
    /// The embedding provider used to be locked along with it and no longer is. It does reach the
    /// signature, so changing it rebuilds the index cleanly -- and DataSourceReindexWarning asks
    /// before it does. Locking it as well left a data source whose provider was deleted stuck on
    /// keyword search for good, with no way back.
    ///
    /// Unclear counts as locked: an unavailable index database says nothing about what is stored.
    /// </remarks>
    /// <param name="dataSourceId">The data source to ask about.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when the source must not be changed.</returns>
    public async Task<bool> ShouldLockDataSourceOriginAsync(string dataSourceId, CancellationToken token = default)
    {
        var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
        if (!indexStore.IsAvailable)
        {
            logger.LogWarning("Locking the source of data source '{DataSourceId}' because the local RAG index database '{DatabaseName}' is unavailable.", dataSourceId, indexStore.Name);
            return true;
        }

        var indexState = await indexStore.GetDataSourceStateAsync(dataSourceId, token);
        return HasStoredIndexState(indexState);
    }

    /// <summary>
    /// Whether the index holds anything at all about a data source.
    /// </summary>
    /// <remarks>
    /// The row of the data source is the whole answer. Everything else the index stores about it --
    /// its documents, their chunks and the documents skipped for good -- hangs on that row and is
    /// deleted along with it, and the row itself is only ever written together with the embedding
    /// provider and the signature. Reading the documents as well, the way the manifest does, would
    /// add nothing but time, and a lot of it for a data source with a hundred thousand documents.
    ///
    /// A data source whose documents were all skipped for good therefore has index state as well,
    /// even though nothing was indexed of it.
    /// </remarks>
    /// <param name="indexState">What the index store holds about the data source as a whole, or null.</param>
    /// <returns>True when there is stored index state.</returns>
    private static bool HasStoredIndexState(DataSourceIndexState? indexState) => indexState is not null;

    /// <summary>
    /// Picks the data sources which already hold something in the index.
    /// </summary>
    /// <remarks>
    /// Asked before a setting is saved which would throw those indexes away, so the question can be
    /// put to the user with the names in it. Anything unclear counts as holding something — the
    /// opposite of IsAwaitingReindexAsync, and for the opposite reason: there, a wrongly greyed-out
    /// row would stay wrong for good, while a question asked once too often costs a click, and one
    /// skipped costs whatever a cloud provider charges for embedding everything again.
    /// </remarks>
    /// <param name="dataSources">The data sources to ask about.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Those of them which have stored index state.</returns>
    public async Task<IReadOnlyList<IDataSourceBase>> GetDataSourcesWithStoredIndexAsync(IReadOnlyCollection<IDataSourceBase> dataSources, CancellationToken token = default)
    {
        //
        // Filtering first also keeps the index database from being created while local RAG is off:
        // asking for the store runs its migrations on the first call, which must not happen because
        // somebody opened a dialog.
        //
        var candidates = dataSources.Where(this.IsSupportedIndexedSource).ToList();
        if (candidates.Count == 0)
            return [];

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(REINDEX_CHECK_TIMEOUT);

            var indexStore = await databaseClientProvider.GetIndexStoreAsync(timeout.Token);
            if (!indexStore.IsAvailable)
            {
                logger.LogWarning("Could not tell which data sources hold a stored index because the local RAG index database '{DatabaseName}' is unavailable. Treating all {DataSourceCount} of them as affected.", indexStore.Name, candidates.Count);
                return candidates;
            }

            var affected = new List<IDataSourceBase>(candidates.Count);
            foreach (var dataSource in candidates)
            {
                var indexState = await indexStore.GetDataSourceStateAsync(dataSource.Id, timeout.Token);
                if (HasStoredIndexState(indexState))
                    affected.Add(dataSource);
            }

            return affected;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not tell which of {DataSourceCount} data source(s) hold a stored index. Treating all of them as affected.", candidates.Count);
            return candidates;
        }
    }

    /// <summary>
    /// Whether a data source cannot answer a search right now because its index has to be built anew.
    /// </summary>
    /// <remarks>
    /// Says nothing about a data source which is only catching up with a handful of changed files:
    /// everything indexed before is still there and still searchable. What this catches is the case
    /// where the whole index was thrown away, or is about to be, because the embedding configuration
    /// changed under it. Between discarding the old vectors and finishing the new ones, the data
    /// source looks perfectly fine and finds nothing.
    ///
    /// Two things are asked, in this order. The stored signature tells whether the vectors still
    /// belong to the current configuration; it is written back right after the reset, so on its own
    /// it would call a rebuild in progress finished. The stored hash of the data source closes that
    /// gap: it survives an ordinary run but not a reset, so an empty one means no run has completed
    /// since the index was discarded.
    ///
    /// Anything unclear counts as not waiting. Whoever asks does so to grey out a row, and a data
    /// source wrongly greyed out for good is worse than one which turns out to have nothing to say.
    /// </remarks>
    /// <param name="dataSource">The data source to ask about.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when the data source is waiting for its index to be rebuilt.</returns>
    public async Task<bool> IsAwaitingReindexAsync(IDataSourceBase dataSource, CancellationToken token = default)
    {
        //
        // This guard also keeps the index database out of the picture while local RAG is switched
        // off: asking for the store creates the database and runs its migrations on the first call,
        // which must not happen because somebody opened the data source selection.
        //
        if (!this.IsSupportedIndexedSource(dataSource))
            return false;

        if (!this.TryResolveEmbeddingProvider(dataSource, out var embeddingProvider))
            return false;

        try
        {
            //
            // A timeout of its own: this runs while the user waits for a popover to open, and the
            // embedding service may be writing to the same database at the time.
            //
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(REINDEX_CHECK_TIMEOUT);

            var indexStore = await databaseClientProvider.GetIndexStoreAsync(timeout.Token);
            if (!indexStore.IsAvailable)
                return false;

            var indexState = await indexStore.GetDataSourceStateAsync(dataSource.Id, timeout.Token);
            var chunkingOptions = GetChunkingOptions(dataSource, embeddingProvider);
            var embeddingSignature = BuildEmbeddingSignature(dataSource, embeddingProvider, chunkingOptions);
            var runState = this.statuses.TryGetValue(dataSource.Id, out var status) ? status.State : (DataSourceEmbeddingState?)null;

            return IsIndexAwaitingRebuild(indexState, embeddingSignature, runState);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not tell whether data source '{DataSourceName}' ({DataSourceId}) is waiting for a re-index. Treating it as usable.", dataSource.Name, dataSource.Id);
            return false;
        }
    }

    /// <summary>
    /// Decides from the stored index state alone whether a data source has to be indexed anew.
    /// </summary>
    /// <remarks>
    /// Kept apart from reading the database so the decision itself can be pinned down in a test.
    /// The order of the three questions is what makes it correct, see IsAwaitingReindexAsync.
    /// </remarks>
    /// <param name="indexState">What the index holds about the data source, or null when it holds nothing.</param>
    /// <param name="currentEmbeddingSignature">The signature the current embedding configuration produces.</param>
    /// <param name="runState">The state of this data source's last or current run, when one is known.</param>
    /// <returns>True when the data source is waiting for its index to be rebuilt.</returns>
    internal static bool IsIndexAwaitingRebuild(DataSourceIndexState? indexState, string currentEmbeddingSignature, DataSourceEmbeddingState? runState)
    {
        // Nothing stored at all: this data source has never been indexed, so there is nothing to
        // search in it yet.
        if (indexState is null)
            return true;

        // The stored vectors belong to another embedding configuration. They will be thrown away
        // as soon as the next run starts, and they are of no use before that either.
        if (!string.Equals(indexState.EmbeddingSignature, currentEmbeddingSignature, StringComparison.Ordinal))
            return true;

        // A run has worked through the whole data source since the index was last discarded.
        if (!string.IsNullOrWhiteSpace(indexState.SourceHash))
            return false;

        //
        // The index was discarded and nothing has finished since. A failed run is the exception:
        // whatever it managed to index is searchable, and the embeddings page already names the
        // problem, so there is nothing to be gained from locking the row as well.
        //
        return runState is not DataSourceEmbeddingState.FAILED;
    }

    /// <summary>
    /// Whether a data source cannot be searched because its vector store cannot be read anymore.
    /// </summary>
    /// <remarks>
    /// Unlike the re-index check above, this reads no database at all: the state comes from the run
    /// or the search which ran into the unreadable store, and is kept in memory only. That it does
    /// not survive a restart is deliberate. The very same store may well open on the next start,
    /// and a mark written to disk would then be wrong with nobody noticing. Until something touches
    /// the store again, the data source counts as usable, and a failing search says so on its own.
    /// </remarks>
    /// <param name="dataSource">The data source to ask about.</param>
    /// <returns>True when the data source waits for the user to have its index rebuilt.</returns>
    public bool NeedsIndexRepair(IDataSourceBase dataSource) =>
        this.statuses.TryGetValue(dataSource.Id, out var status) &&
        status is { State: DataSourceEmbeddingState.FAILED, VectorStoreUnreadable: true };

    public Task QueueDataSourceAsync(IDataSourceBase dataSource)
    {
        return this.QueueDataSourceAsync(dataSource, true, DataSourceEmbeddingRefreshMode.HASH_CHECK);
    }

    public Task QueueDataSourceAsync(string dataSourceId)
    {
        return this.TryGetConfiguredIndexedSource(dataSourceId, out var dataSource)
            ? this.QueueDataSourceAsync(dataSource)
            : Task.CompletedTask;
    }

    public Task RetryDataSourceAsync(string dataSourceId)
    {
        return this.TryGetConfiguredIndexedSource(dataSourceId, out var dataSource)
            ? this.QueueDataSourceAsync(dataSource, true, DataSourceEmbeddingRefreshMode.MANUAL_RETRY)
            : Task.CompletedTask;
    }

    private async Task QueueDataSourceAsync(IDataSourceBase dataSource, bool queueAfterCurrentRun, DataSourceEmbeddingRefreshMode refreshMode)
    {
        if (!this.IsSupportedIndexedSource(dataSource))
            return;

        this.RefreshWatchers();
        logger.LogDebug("Refreshed watcher state for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);

        var queueRequestResult = this.TryReserveDataSourceQueueSlot(dataSource.Id, queueAfterCurrentRun, refreshMode);
        switch (queueRequestResult)
        {
            case DataSourceQueueRequestResult.ALREADY_QUEUED:
                logger.LogDebug("Data source '{DataSourceName}' ({DataSourceId}) is already queued for background embeddings. Ignoring duplicate queue request.", dataSource.Name, dataSource.Id);
                return;

            case DataSourceQueueRequestResult.RUNNING:
                logger.LogDebug("Data source '{DataSourceName}' ({DataSourceId}) is already being embedded. Ignoring duplicate queue request.", dataSource.Name, dataSource.Id);
                return;

            case DataSourceQueueRequestResult.RUNNING_MARKED_PENDING:
                logger.LogDebug("Data source '{DataSourceName}' ({DataSourceId}) is already being embedded. Scheduled one follow-up embedding run.", dataSource.Name, dataSource.Id);
                return;
        }

        logger.LogInformation(
            "Queueing data source '{DataSourceName}' ({DataSourceId}) for background embedding hash check. RefreshMode={RefreshMode}.",
            dataSource.Name,
            dataSource.Id,
            refreshMode);
        if (!this.statuses.TryGetValue(dataSource.Id, out var currentStatus) || currentStatus.State is not DataSourceEmbeddingState.RUNNING)
        {
            this.UpsertStatus(this.CreateStatus(
                dataSource,
                DataSourceEmbeddingState.QUEUED,
                currentStatus?.TotalDocuments ?? 0,
                currentStatus?.IndexedDocuments ?? 0,
                currentStatus?.FailedDocuments ?? 0,
                failures: currentStatus?.Failures ?? [],
                lastSyncUtc: currentStatus?.LastSyncUtc));
        }
        logger.LogDebug("Upserting status for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
        await this.queue.Writer.WriteAsync(new DataSourceEmbeddingQueueItem(dataSource.Id, refreshMode));
        logger.LogDebug("Queued data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
    }

    public async Task RemoveDataSourceAsync(IDataSourceBase dataSource)
    {
        if (!this.IsSupportedIndexedSource(dataSource))
            return;

        this.RemoveWatcher(dataSource.Id);
        var activeRun = this.CancelActiveDataSourceRun(dataSource);
        this.ClearQueuedDataSourceState(dataSource.Id);
        this.statuses.TryRemove(dataSource.Id, out _);
        if (activeRun is not null)
        {
            logger.LogInformation(
                "Waiting for the active embedding run for deleted data source '{DataSourceName}' ({DataSourceId}) to stop before deleting persisted embeddings.",
                dataSource.Name,
                dataSource.Id);
            await activeRun.Completion.Task;
        }

        this.statuses.TryRemove(dataSource.Id, out _);
        await this.ResetPersistedStateAsync(dataSource.Id, null, null, CancellationToken.None);
        this.statuses.TryRemove(dataSource.Id, out _);
        PublishStatusChanged();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.WaitForInitialSettingsAndBootstrapAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var queueItem = await this.queue.Reader.ReadAsync(stoppingToken);
            var dataSourceId = queueItem.DataSourceId;
            this.MarkDataSourceRunStarted(dataSourceId);

            IIndexedDataSource? dataSource = null;

            try
            {
                if (!this.TryGetConfiguredIndexedSource(dataSourceId, out dataSource))
                    continue;

                await this.ProcessDataSourceRunAsync(dataSource, queueItem.RefreshMode, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (VectorStoreUnreadableException exception) when (dataSource is not null)
            {
                //
                // Nothing is deleted and nothing is rebuilt here. The data source says what is
                // wrong with it, stays out of the selection while it says so, and waits for the
                // user to ask for the repair.
                //
                logger.LogError(
                    exception,
                    "The vector store of data source '{DataSourceName}' ({DataSourceId}) cannot be read. The data source is waiting for a repair.",
                    dataSource.Name,
                    dataSource.Id);
                this.UpsertStatus(this.GetUnreadableVectorStoreStatus(dataSource));
            }
            catch (Exception exception)
            {
                if (dataSource is null)
                {
                    logger.LogError(exception, "Background embedding failed for data source '{DataSourceId}'.", dataSourceId);
                }
                else
                {
                    logger.LogError(exception, "Background embedding failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
                    this.UpsertStatus(this.GetFallbackStatus(dataSource, string.Format(TB("The data source '{0}' could not be processed. The log file holds the details."), dataSource.Name)));
                }
            }
            finally
            {
                await this.QueuePendingDataSourceRunAsync(dataSourceId, stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        this.DisposeWatchers();
        base.Dispose();
    }

    private async Task ProcessDataSourceRunAsync(IDataSourceBase requestedDataSource, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken parentToken)
    {
        if (!this.TryGetConfiguredIndexedSource(requestedDataSource.Id, out var dataSource))
        {
            logger.LogDebug(
                "Skipping embedding run for data source '{DataSourceName}' ({DataSourceId}) because it is no longer configured. RefreshMode={RefreshMode}.",
                requestedDataSource.Name,
                requestedDataSource.Id,
                refreshMode);
            return;
        }

        var runTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        var runControl = new DataSourceRunControl(
            runTokenSource,
            new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously));

        if (!this.activeRuns.TryAdd(dataSource.Id, runControl))
        {
            runTokenSource.Dispose();
            logger.LogDebug(
                "Data source '{DataSourceName}' ({DataSourceId}) already has an active embedding run. Skipping duplicate process request. RefreshMode={RefreshMode}.",
                dataSource.Name,
                dataSource.Id,
                refreshMode);
            return;
        }

        try
        {
            await this.ProcessDataSourceAsync(dataSource, refreshMode, runTokenSource.Token);
        }
        catch (OperationCanceledException) when (!parentToken.IsCancellationRequested && runTokenSource.IsCancellationRequested)
        {
            logger.LogInformation(
                "Stopped background embeddings for data source '{DataSourceName}' ({DataSourceId}) because the data source was removed or canceled. RefreshMode={RefreshMode}.",
                dataSource.Name,
                dataSource.Id,
                refreshMode);
        }
        finally
        {
            this.activeRuns.TryRemove(dataSource.Id, out _);
            runControl.Completion.TrySetResult(null);
            runTokenSource.Dispose();
        }
    }

    /// <summary>
    /// Works out everything an indexing run needs, before anything is read from the data source.
    /// </summary>
    /// <remarks>
    /// The same for every kind of data source: both stores have to be there, the embedding provider
    /// has to exist and meet the confidence level the data source asks for, and the stored manifest
    /// has to belong to the current embedding configuration -- otherwise it is discarded here. When
    /// any of this fails, the status of the data source says why, and there is no run.
    /// </remarks>
    /// <param name="dataSource">The data source to index.</param>
    /// <param name="refreshMode">Why the run was started, for the log.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The context of the run, or null when there is no run.</returns>
    private async Task<IndexedRunContext?> PrepareIndexedRunAsync(IIndexedDataSource dataSource, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        logger.LogInformation(
            "Starting background embedding hash check for data source '{DataSourceName}' ({DataSourceId}). RefreshMode={RefreshMode}.",
            dataSource.Name,
            dataSource.Id,
            refreshMode);
        token.ThrowIfCancellationRequested();

        var vectorStore = await databaseClientProvider.GetVectorStoreAsync(token);
        var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
        token.ThrowIfCancellationRequested();

        if (!vectorStore.IsAvailable)
        {
            logger.LogWarning(
                "Skipping background embeddings for data source '{DataSourceName}' ({DataSourceId}) because the database client '{DatabaseName}' is unavailable.",
                dataSource.Name,
                dataSource.Id,
                vectorStore.Name);
            token.ThrowIfCancellationRequested();
            this.UpsertStatus(this.GetFallbackStatus(dataSource, TB("The vector database is not available.")));
            return null;
        }

        if (!indexStore.IsAvailable)
        {
            logger.LogWarning(
                "Skipping background embeddings for data source '{DataSourceName}' ({DataSourceId}) because the database client '{DatabaseName}' is unavailable.",
                dataSource.Name,
                dataSource.Id,
                indexStore.Name);
            token.ThrowIfCancellationRequested();
            this.UpsertStatus(this.GetFallbackStatus(dataSource, TB("The local RAG index database is not available.")));
            return null;
        }

        var collectionName = DataSourceEmbeddingNames.GetCollectionName(dataSource.Id);
        var persistedState = await indexStore.GetDataSourceStateAsync(dataSource.Id, token);
        if (persistedState is { VectorSize: > 0 })
        {
            var ensureResult = await vectorStore.EnsureVectorStoreExists(collectionName, dataSource.Name, persistedState.VectorSize, token);
            if (ensureResult.Created)
            {
                logger.LogWarning(
                    "Vector store '{CollectionName}' for data source '{DataSourceName}' ({DataSourceId}) was missing although persisted embedding state exists. Resetting the stale state so all vectors are rebuilt.",
                    collectionName,
                    dataSource.Name,
                    dataSource.Id);
                await this.ResetPersistedStateAsync(dataSource.Id, vectorStore, indexStore, token);
            }
        }

        if (!this.TryResolveEmbeddingProvider(dataSource, out var embeddingProvider))
        {
            token.ThrowIfCancellationRequested();
            this.UpsertStatus(this.GetFallbackStatus(dataSource, TB("The selected embedding provider is not available. Please check it in the settings.")));
            return null;
        }

        if (!AllowsEmbedding(dataSource, embeddingProvider.GetConfidenceLevel(settingsManager)))
        {
            var errorMessage = dataSource is DataSourceMailbox && !dataSource.ConfidenceLevel.IsAllowedMailboxConfidence()
                ? TB("The mailbox has no valid confidence level, so no provider may read it. Please choose one in the settings of the mailbox.")
                : string.Format(TB("The selected embedding provider is not allowed to index this data source. The data source asks for the confidence level '{0}', while the embedding provider has '{1}'."), dataSource.ConfidenceLevel.GetName(), embeddingProvider.GetConfidenceLevel(settingsManager).GetName());
            logger.LogWarning(
                "Skipping background embeddings for data source '{DataSourceName}' ({DataSourceId}) because embedding provider '{EmbeddingProviderName}' ({EmbeddingProviderId}) does not meet the required confidence. RequiredConfidence={RequiredConfidence}, EmbeddingProviderConfidence={EmbeddingProviderConfidence}.",
                dataSource.Name,
                dataSource.Id,
                embeddingProvider.Name,
                embeddingProvider.Id,
                dataSource.ConfidenceLevel.GetName(),
                embeddingProvider.GetConfidenceLevel(settingsManager).GetName());

            token.ThrowIfCancellationRequested();
            this.UpsertStatus(this.GetFallbackStatus(dataSource, errorMessage));
            return null;
        }

        logger.LogInformation(
            "Using embedding provider '{EmbeddingProviderId}' with model '{EmbeddingModelId}' for data source '{DataSourceName}' ({DataSourceId}).",
            embeddingProvider.Id,
            embeddingProvider.Model.Id,
            dataSource.Name,
            dataSource.Id);

        var manifest = await this.EnsureCompatibleManifestAsync(dataSource, embeddingProvider, collectionName, vectorStore, indexStore, token);
        token.ThrowIfCancellationRequested();

        return new IndexedRunContext(dataSource, embeddingProvider, embeddingProvider.CreateProvider(), vectorStore, indexStore, manifest, settingsManager, this.UpsertStatus, logger);
    }

    private async Task ProcessDataSourceAsync(IIndexedDataSource dataSource, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        if (!this.TryGetIndexer(dataSource, out var indexer))
        {
            logger.LogWarning(
                "Skipping background embeddings for data source '{DataSourceName}' ({DataSourceId}) because no indexer reads this kind of data source.",
                dataSource.Name,
                dataSource.Id);
            return;
        }

        var context = await this.PrepareIndexedRunAsync(dataSource, refreshMode, token);
        if (context is null)
            return;

        //
        // Queued behind whatever else waits, so a data source which takes hours gives the others
        // their turn. While this run is still active, that is the follow-up run every request
        // during a run leaves behind. Should the user ask for a run of their own in the meantime,
        // theirs comes first, and carries on just the same.
        //
        if (await indexer.ProcessAsync(context, refreshMode, token) is IndexedRunOutcome.MORE_TO_DO)
            await this.QueueDataSourceAsync(dataSource, true, DataSourceEmbeddingRefreshMode.CONTINUATION);
    }

    private async Task DeleteCollectionAsync(string collectionName, VectorStoreClient? vectorStore, CancellationToken token)
    {
        vectorStore ??= await databaseClientProvider.GetVectorStoreAsync(token);
        if (!vectorStore.IsAvailable)
        {
            logger.LogWarning("Could not delete embedding collection '{CollectionName}' because the vector store '{VectorStoreName}' is unavailable.", collectionName, vectorStore.Name);
            return;
        }

        await vectorStore.DeleteVectorStore(collectionName, token);
    }

    private async Task WaitForInitialSettingsAndBootstrapAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (settingsManager.HasCompletedInitialSettingsLoad
                && !string.IsNullOrWhiteSpace(SettingsManager.ConfigDirectory)
                && !string.IsNullOrWhiteSpace(SettingsManager.DataDirectory))
            {
                break;
            }

            await Task.Delay(250, token);
        }

        token.ThrowIfCancellationRequested();

        logger.LogInformation("Embedding background service is ready. Running the initial persisted hash check before activating file watchers.");
        await this.RunInitialDataSourceHashCheckAsync(token);
    }

    private async Task RunInitialDataSourceHashCheckAsync(CancellationToken token)
    {
        if (!settingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh)
        {
            logger.LogInformation("Automatic local data source refresh is disabled. Startup hash checks and file watchers are disabled.");
            this.RemoveAllWatchers();
            return;
        }

        if (Interlocked.Exchange(ref this.startupHashCheckStarted, 1) == 1)
            return;

        this.RemoveAllWatchers();

        var supportedDataSources = this.GetConfiguredIndexedSources();

        logger.LogInformation(
            "Starting initial persisted hash check for {DataSourceCount} supported internal data source(s). Incomplete or failed local RAG embedding state will be retried during this pass. File watchers will be activated after this check completes.",
            supportedDataSources.Count);

        //
        // Every data source gets its row before the first run starts. This pass works through them
        // one after the other, and re-indexing a large source takes its time: without this, the
        // embeddings page would show the one source being worked on and nothing else, which reads
        // as if the others were gone rather than waiting their turn. The queueing path does the
        // same thing when it reserves a slot, which is why it never had this problem.
        //
        foreach (var dataSource in supportedDataSources)
        {
            if (this.statuses.TryGetValue(dataSource.Id, out var knownStatus) && knownStatus.State is DataSourceEmbeddingState.RUNNING)
                continue;

            this.statuses[dataSource.Id] = this.CreateStatus(
                dataSource,
                DataSourceEmbeddingState.QUEUED,
                knownStatus?.TotalDocuments ?? 0,
                knownStatus?.IndexedDocuments ?? 0,
                knownStatus?.FailedDocuments ?? 0,
                failures: knownStatus?.Failures ?? [],
                permanentlySkippedDocuments: knownStatus?.PermanentlySkippedDocuments ?? 0,
                lastSyncUtc: knownStatus?.LastSyncUtc);
        }

        // One message for the whole list, rather than one per data source:
        PublishStatusChanged();

        foreach (var dataSource in supportedDataSources)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await this.ProcessDataSourceRunAsync(dataSource, DataSourceEmbeddingRefreshMode.STARTUP_HASH_CHECK, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (VectorStoreUnreadableException exception)
            {
                logger.LogError(
                    exception,
                    "The vector store of data source '{DataSourceName}' ({DataSourceId}) cannot be read. The data source is waiting for a repair.",
                    dataSource.Name,
                    dataSource.Id);
                this.UpsertStatus(this.GetUnreadableVectorStoreStatus(dataSource));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Initial embedding hash check failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
                this.UpsertStatus(this.GetFallbackStatus(dataSource, string.Format(TB("The data source '{0}' could not be processed. The log file holds the details."), dataSource.Name)));
            }
        }

        if (!settingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh)
        {
            Volatile.Write(ref this.startupHashCheckCompleted, 0);
            Interlocked.Exchange(ref this.startupHashCheckStarted, 0);
            logger.LogInformation("Automatic local data source refresh was disabled before the initial hash check completed. File watchers remain inactive.");
            this.RemoveAllWatchers();
            return;
        }

        Volatile.Write(ref this.startupHashCheckCompleted, 1);
        logger.LogInformation("Completed initial persisted hash check. Activating file watchers for automatic local data source refresh.");
        this.RefreshWatchers();
    }

    private bool IsSupportedIndexedSource(IDataSourceBase dataSource)
    {
        //
        // Local RAG is a preview feature, so nothing here may run while it is switched off. This is
        // the one place to decide that: every path which scans files, starts a watcher, creates the
        // index database or sends text to an embedding provider asks this question first.
        //
        // Checking the feature instead of relying on "no data sources configured" also covers the
        // case where somebody enabled the feature, configured local data sources, and switched the
        // feature off again. Their data sources stay in the settings, and without this check the
        // service would keep indexing them.
        //
        if (!PreviewFeatures.PRE_RAG_2024.IsEnabled(settingsManager))
            return false;

        // Mailboxes are a preview of their own, on top of local RAG:
        if (dataSource is DataSourceMailbox && !PreviewFeatures.PRE_MAILBOXES_2026.IsEnabled(settingsManager))
            return false;

        return this.TryGetIndexer(dataSource, out _);
    }

    /// <summary>
    /// Whether an embedding provider of the given confidence level may read a data source.
    /// </summary>
    /// <remarks>
    /// A mailbox is stricter than the other data sources: without a level of its own, it is closed
    /// to every provider rather than open to all, cf. AllowsMailboxConfidenceLevel.
    /// </remarks>
    /// <param name="dataSource">The data source to index.</param>
    /// <param name="embeddingProviderConfidence">The confidence level of the embedding provider.</param>
    /// <returns>True when the provider may embed the content of the data source.</returns>
    internal static bool AllowsEmbedding(IIndexedDataSource dataSource, ConfidenceLevel embeddingProviderConfidence) => dataSource is DataSourceMailbox
        ? embeddingProviderConfidence.AllowsMailboxConfidenceLevel(dataSource.ConfidenceLevel)
        : embeddingProviderConfidence.AllowsDataSourceConfidenceLevel(dataSource.ConfidenceLevel);

    /// <summary>
    /// The configured data sources this service indexes, from every list which holds some.
    /// </summary>
    /// <remarks>
    /// The one place which knows where data sources are kept: in DataSources those which classic
    /// RAG and the agents see as well, in Mailboxes those which only the mail tools read. Whatever
    /// works through all of them, or looks one up by its id, goes through here.
    /// </remarks>
    /// <returns>The data sources, those from DataSources first.</returns>
    private IReadOnlyList<IIndexedDataSource> GetConfiguredIndexedSources() => settingsManager.ConfigurationData.DataSources
        .OfType<IIndexedDataSource>()
        .Concat(settingsManager.ConfigurationData.Mailboxes.Select(mailbox => (IIndexedDataSource)mailbox))
        .Where(this.IsSupportedIndexedSource)
        .ToList();

    /// <summary>
    /// Finds the indexer which reads a data source.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="indexer">The indexer, when there is one for this kind of data source.</param>
    /// <returns>True when an indexer was found.</returns>
    private bool TryGetIndexer(IDataSourceBase dataSource, [NotNullWhen(true)] out IIndexedSourceIndexer? indexer)
    {
        indexer = this.indexers.FirstOrDefault(candidate => candidate.Supports(dataSource));
        return indexer is not null;
    }

    /// <summary>
    /// Finds the configured data source this service indexes under a given id.
    /// </summary>
    /// <remarks>
    /// The one place which looks a data source up by its id. Every run, every follow-up and every
    /// request from the UI goes through here, so a data source which is no longer configured, or
    /// which this service does not index, is turned away the same way everywhere.
    /// </remarks>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <param name="dataSource">The data source, when it is configured and indexed by this service.</param>
    /// <returns>True when such a data source was found.</returns>
    private bool TryGetConfiguredIndexedSource(string dataSourceId, [NotNullWhen(true)] out IIndexedDataSource? dataSource)
    {
        dataSource = this.GetConfiguredIndexedSources().FirstOrDefault(source => source.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase));
        return dataSource is not null;
    }

    private bool TryResolveEmbeddingProvider(IDataSourceBase dataSource, [NotNullWhen(true)] out EmbeddingProvider? embeddingProvider)
        => DataSourceEmbeddingProviders.TryResolve(settingsManager, dataSource, out embeddingProvider);

    private async Task<DataSourceEmbeddingManifest> EnsureCompatibleManifestAsync(
        IIndexedDataSource dataSource,
        EmbeddingProvider embeddingProvider,
        string collectionName,
        VectorStoreClient vectorStore,
        IndexStoreClient indexStore,
        CancellationToken token)
    {
        var chunkingOptions = GetChunkingOptions(dataSource, embeddingProvider);
        var embeddingSignature = BuildEmbeddingSignature(dataSource, embeddingProvider, chunkingOptions);
        var manifest = await indexStore.GetManifestAsync(dataSource.Id, token);

        logger.LogInformation(
            "Loaded persisted local RAG index manifest for data source '{DataSourceName}' ({DataSourceId}). StoredFiles={StoredFiles}, StoredPermanentFailures={StoredPermanentFailures}, StoredSourceHashPrefix={StoredSourceHashPrefix}, StoredSignaturePrefix={StoredSignaturePrefix}, CurrentSignaturePrefix={CurrentSignaturePrefix}.",
            dataSource.Name,
            dataSource.Id,
            manifest.Files.Count,
            manifest.PermanentFailures.Count,
            ShortHash(manifest.SourceHash),
            ShortHash(manifest.EmbeddingSignature),
            ShortHash(embeddingSignature));

        if (!string.Equals(manifest.EmbeddingSignature, embeddingSignature, StringComparison.Ordinal))
        {
            logger.LogInformation(
                "Embedding configuration changed for data source '{DataSourceName}' ({DataSourceId}). Resetting persisted embedding state and collection '{CollectionName}'.",
                dataSource.Name,
                dataSource.Id,
                collectionName);
            logger.LogDebug(
                "Embedding signature mismatch for data source '{DataSourceName}' ({DataSourceId}). StoredSignature='{StoredEmbeddingSignature}', CurrentSignature='{CurrentEmbeddingSignature}'.",
                dataSource.Name,
                dataSource.Id,
                manifest.EmbeddingSignature,
                embeddingSignature);
            await this.ResetPersistedStateAsync(dataSource.Id, vectorStore, indexStore, token);
            manifest = await indexStore.GetManifestAsync(dataSource.Id, token);
        }

        if (!string.Equals(manifest.EmbeddingProviderId, embeddingProvider.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(manifest.EmbeddingSignature, embeddingSignature, StringComparison.Ordinal))
        {
            manifest.EmbeddingProviderId = embeddingProvider.Id;
            manifest.EmbeddingSignature = embeddingSignature;
        }

        await indexStore.UpsertDataSourceAsync(
            dataSource.Id,
            dataSource.Type.ToString(),
            manifest.EmbeddingProviderId,
            manifest.EmbeddingSignature,
            manifest.SourceHash,
            manifest.VectorSize,
            token);

        return manifest;
    }

    private DataSourceEmbeddingStatus CreateStatus(
        IDataSourceBase dataSource,
        DataSourceEmbeddingState state,
        int totalDocuments,
        int indexedDocuments,
        int failedDocuments,
        string currentDocument = "",
        string lastError = "",
        IReadOnlyList<DataSourceEmbeddingFailure>? failures = null,
        int permanentlySkippedDocuments = 0,
        int? currentDocumentBlock = null,
        int? currentDocumentPage = null,
        bool vectorStoreUnreadable = false,
        DateTimeOffset? lastSyncUtc = null)
    {
        return new DataSourceEmbeddingStatus(
            dataSource.Id,
            dataSource.Name,
            dataSource.Type,
            state,
            totalDocuments,
            indexedDocuments,
            failedDocuments,
            currentDocument,
            lastError,
            failures?.ToList() ?? [],
            permanentlySkippedDocuments,
            currentDocumentBlock,
            currentDocumentPage,
            vectorStoreUnreadable,
            LastSyncUtc: lastSyncUtc);
    }

    private DataSourceEmbeddingStatus GetFallbackStatus(IDataSourceBase dataSource, string errorMessage)
    {
        return this.CreateStatus(
            dataSource,
            DataSourceEmbeddingState.FAILED,
            0,
            0,
            1,
            lastError: errorMessage,
            failures: [new DataSourceEmbeddingFailure(dataSource.Name, errorMessage, DateTimeOffset.UtcNow)]);
    }

    /// <remarks>
    /// Deliberately not the message which came from the runtime: that one names a store name and a
    /// path, is written in English for the log file, and says nothing about what happens next. What
    /// the user needs to read is what this means for their chats and where the way out is.
    /// </remarks>
    private DataSourceEmbeddingStatus GetUnreadableVectorStoreStatus(IDataSourceBase dataSource)
    {
        var errorMessage = string.Format(TB("The index of the data source '{0}' cannot be read anymore. The data source stays out of your chats until its index was built anew. Use the repair action to start that."), dataSource.Name);
        return this.CreateStatus(
            dataSource,
            DataSourceEmbeddingState.FAILED,
            0,
            0,
            1,
            lastError: errorMessage,
            failures: [new DataSourceEmbeddingFailure(dataSource.Name, errorMessage, DateTimeOffset.UtcNow)],
            vectorStoreUnreadable: true);
    }

    /// <remarks>
    /// A request arriving while the data source is being embedded leaves a mark for one follow-up
    /// run, and the mark keeps why it was asked for. The first request decides that: any later one
    /// only confirms that a follow-up is needed, which it already is.
    /// </remarks>
    private DataSourceQueueRequestResult TryReserveDataSourceQueueSlot(string dataSourceId, bool queueAfterCurrentRun, DataSourceEmbeddingRefreshMode refreshMode)
    {
        lock (this.queueStateLock)
        {
            if (this.runningIds.ContainsKey(dataSourceId))
            {
                if (queueAfterCurrentRun && this.pendingRefreshModes.TryAdd(dataSourceId, refreshMode))
                    return DataSourceQueueRequestResult.RUNNING_MARKED_PENDING;

                return DataSourceQueueRequestResult.RUNNING;
            }

            if (!this.queuedIds.TryAdd(dataSourceId, 0))
                return DataSourceQueueRequestResult.ALREADY_QUEUED;

            return DataSourceQueueRequestResult.QUEUED;
        }
    }

    private void MarkDataSourceRunStarted(string dataSourceId)
    {
        lock (this.queueStateLock)
        {
            this.queuedIds.TryRemove(dataSourceId, out _);
            this.runningIds.TryAdd(dataSourceId, 0);
        }
    }

    private bool TryCompleteDataSourceRun(string dataSourceId, bool allowPendingRequeue, out DataSourceEmbeddingRefreshMode pendingRefreshMode)
    {
        lock (this.queueStateLock)
        {
            this.runningIds.TryRemove(dataSourceId, out _);

            if (!this.pendingRefreshModes.TryRemove(dataSourceId, out pendingRefreshMode))
                return false;

            return allowPendingRequeue && this.queuedIds.TryAdd(dataSourceId, 0);
        }
    }

    private void ReleaseQueuedDataSourceRun(string dataSourceId)
    {
        lock (this.queueStateLock)
        {
            this.queuedIds.TryRemove(dataSourceId, out _);
        }
    }

    private void ClearQueuedDataSourceState(string dataSourceId)
    {
        lock (this.queueStateLock)
        {
            this.queuedIds.TryRemove(dataSourceId, out _);
            this.pendingRefreshModes.TryRemove(dataSourceId, out _);
        }
    }

    private DataSourceRunControl? CancelActiveDataSourceRun(IDataSourceBase dataSource)
    {
        if (!this.activeRuns.TryGetValue(dataSource.Id, out var activeRun))
            return null;

        logger.LogInformation(
            "Canceling active embedding run for deleted data source '{DataSourceName}' ({DataSourceId}).",
            dataSource.Name,
            dataSource.Id);
        try
        {
            activeRun.TokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return null;
        }

        return activeRun;
    }

    private async Task QueuePendingDataSourceRunAsync(string dataSourceId, CancellationToken token)
    {
        IIndexedDataSource? dataSource = null;
        var isConfigured = !token.IsCancellationRequested && this.TryGetConfiguredIndexedSource(dataSourceId, out dataSource);
        if (!this.TryCompleteDataSourceRun(dataSourceId, isConfigured, out var refreshMode))
            return;

        if (dataSource is null)
        {
            this.ReleaseQueuedDataSourceRun(dataSourceId);
            return;
        }

        logger.LogInformation("Queueing one follow-up embedding run for data source '{DataSourceName}' ({DataSourceId}) after changes arrived during the previous run. RefreshMode={RefreshMode}.", dataSource.Name, dataSource.Id, refreshMode);

        this.statuses.TryGetValue(dataSource.Id, out var currentStatus);
        this.UpsertStatus(this.CreateStatus(
            dataSource,
            DataSourceEmbeddingState.QUEUED,
            currentStatus?.TotalDocuments ?? 0,
            currentStatus?.IndexedDocuments ?? 0,
            currentStatus?.FailedDocuments ?? 0,
            lastError: currentStatus?.LastError ?? string.Empty,
            failures: currentStatus?.Failures ?? [],
            lastSyncUtc: currentStatus?.LastSyncUtc));

        try
        {
            await this.queue.Writer.WriteAsync(new DataSourceEmbeddingQueueItem(dataSourceId, refreshMode), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            this.ReleaseQueuedDataSourceRun(dataSourceId);
        }
    }

    private void UpsertStatus(DataSourceEmbeddingStatus status)
    {
        this.statuses[status.DataSourceId] = status;
        PublishStatusChanged();
    }

    private static void PublishStatusChanged()
    {
        _ = MessageBus.INSTANCE.SendMessage(null, Event.RAG_EMBEDDING_STATUS_CHANGED, true);
    }
}