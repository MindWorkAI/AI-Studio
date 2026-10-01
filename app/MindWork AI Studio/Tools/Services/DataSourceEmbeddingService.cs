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

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService(SettingsManager settingsManager, RustService rustService, DatabaseClientProvider databaseClientProvider,
    PromptInjectionGuardService guardService, ILogger<DataSourceEmbeddingService> logger) : BackgroundService
{
    /// <summary>
    /// How often the block progress within one file is reported to the user interface at most.
    /// </summary>
    private static readonly TimeSpan BLOCK_PROGRESS_INTERVAL = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long the re-index check waits for the index database before it gives up.
    /// </summary>
    /// <remarks>
    /// Asked while somebody waits for the data source selection to open, and possibly while a run
    /// writes to the same database.
    /// </remarks>
    private static readonly TimeSpan REINDEX_CHECK_TIMEOUT = TimeSpan.FromSeconds(2);

    private readonly Channel<DataSourceEmbeddingQueueItem> queue = Channel.CreateUnbounded<DataSourceEmbeddingQueueItem>();
    private readonly ConcurrentDictionary<string, byte> queuedIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> runningIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> pendingQueueIds = new(StringComparer.OrdinalIgnoreCase);
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

    private enum DataSourceEmbeddingRefreshMode
    {
        STARTUP_HASH_CHECK,
        HASH_CHECK,
        WATCHER_HASH_CHECK,
        MANUAL_RETRY,
    }

    private sealed record DataSourceEmbeddingQueueItem(string DataSourceId, DataSourceEmbeddingRefreshMode RefreshMode);

    private sealed record DataSourceRunControl(CancellationTokenSource TokenSource, TaskCompletionSource<object?> Completion);

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
            var total = Math.Max(activeStatus.TotalFiles, 1);
            return new(
                activeStatus.State,
                activeStatus.IndexedFiles,
                total,
                activeStatus.FailedFiles);
        }

        var failedStatus = orderedStatuses
            .FirstOrDefault(status => status.State is DataSourceEmbeddingState.FAILED || status.FailedFiles > 0);

        if (failedStatus is not null)
            return new(DataSourceEmbeddingState.FAILED, failedStatus.IndexedFiles, failedStatus.TotalFiles, failedStatus.FailedFiles);

        return new(DataSourceEmbeddingState.COMPLETED, 0, 0, 0);
    }

    public Task QueueAllInternalDataSourcesAsync()
    {
        return this.QueueAllInternalDataSourcesAsync(true);
    }

    private Task QueueAllInternalDataSourcesAsync(bool queueAfterCurrentRun)
    {
        this.RefreshWatchers();

        var supportedDataSources = settingsManager.ConfigurationData.DataSources
            .Where(this.IsSupportedIndexedSource)
            .ToList();

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

        var queueRequestResult = this.TryReserveDataSourceQueueSlot(dataSource.Id, queueAfterCurrentRun);
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
                currentStatus?.TotalFiles ?? 0,
                currentStatus?.IndexedFiles ?? 0,
                currentStatus?.FailedFiles ?? 0,
                failures: currentStatus?.Failures ?? []));
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
        this.PublishStatusChanged();
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

        if (!embeddingProvider.GetConfidenceLevel(settingsManager).AllowsDataSourceConfidenceLevel(dataSource.ConfidenceLevel))
        {
            var errorMessage = string.Format(TB("The selected embedding provider is not allowed to index this data source. The data source asks for the confidence level '{0}', while the embedding provider has '{1}'."), dataSource.ConfidenceLevel.GetName(), embeddingProvider.GetConfidenceLevel(settingsManager).GetName());
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

        return new IndexedRunContext(dataSource, embeddingProvider, embeddingProvider.CreateProvider(), vectorStore, indexStore, manifest, settingsManager, logger);
    }

    private async Task ProcessDataSourceAsync(IIndexedDataSource indexedDataSource, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        if (indexedDataSource is not IInternalDataSource dataSource)
        {
            logger.LogWarning(
                "Skipping background embeddings for non-internal data source '{DataSourceName}' ({DataSourceId}).",
                indexedDataSource.Name,
                indexedDataSource.Id);
            return;
        }

        var context = await this.PrepareIndexedRunAsync(dataSource, refreshMode, token);
        if (context is null)
            return;

        var manifest = context.Manifest;

        var inputFiles = this.GetInputFiles(dataSource);
        var indexedFiles = inputFiles.Files;
        var totalFiles = indexedFiles.Count + inputFiles.FailedFiles;

        foreach (var failure in inputFiles.Failures)
        {
            logger.LogWarning(
                "Cannot index data source input '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}). Reason='{Reason}'.",
                failure.FilePath,
                dataSource.Name,
                dataSource.Id,
                failure.Reason);
        }

        logger.LogInformation(
            "Prepared data source '{DataSourceName}' ({DataSourceId}) for embedding. AccessibleFiles={AccessibleFiles}, FailedFiles={FailedFiles}, Collection='{CollectionName}'.",
            dataSource.Name,
            dataSource.Id,
            indexedFiles.Count,
            inputFiles.FailedFiles,
            context.CollectionName);

        var metadataSnapshot = this.BuildDataSourceMetadataSnapshot(dataSource, indexedFiles);
        var removedMissingFiles = await this.RemoveMissingFileEmbeddingsAsync(context, indexedFiles, token);
        token.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Compared data source hash for '{DataSourceName}' ({DataSourceId}). StoredSourceHashPrefix={StoredSourceHashPrefix}, CurrentSourceHashPrefix={CurrentSourceHashPrefix}, StoredFileRecords={StoredFileRecords}, CurrentFiles={CurrentFiles}, RemovedMissingFiles={RemovedMissingFiles}.",
            dataSource.Name,
            dataSource.Id,
            ShortHash(manifest.SourceHash),
            ShortHash(metadataSnapshot.SourceHash),
            manifest.Files.Count,
            indexedFiles.Count,
            removedMissingFiles);

        if (this.CanSkipDataSourceByHash(manifest, metadataSnapshot, indexedFiles))
        {
            logger.LogInformation(
                "Skipping data source '{DataSourceName}' ({DataSourceId}) because the persisted data source hash and all persisted file hashes match. RefreshMode={RefreshMode}, PermanentlySkippedFiles={PermanentlySkippedFiles}.",
                dataSource.Name,
                dataSource.Id,
                refreshMode,
                manifest.PermanentFailures.Count);

            await context.OptimizeCollectionIfNeededAsync("data source finished after removing missing files", token);

            token.ThrowIfCancellationRequested();
            await context.IndexStore.UpdateDataSourceHashAsync(dataSource.Id, metadataSnapshot.SourceHash, token);

            //
            // The files which were skipped for good are none of the indexed ones, and their stored
            // reasons belong into the list even on a run which read nothing at all:
            //
            this.UpsertStatus(this.CreateCompletedStatus(
                dataSource,
                totalFiles,
                indexedFiles.Count - manifest.PermanentFailures.Count,
                inputFiles.FailedFiles,
                inputFiles.LastError,
                [..inputFiles.Failures, ..CreatePermanentFailureDetails(manifest)],
                manifest.PermanentFailures.Count));
            return;
        }

        token.ThrowIfCancellationRequested();
        this.UpsertStatus(this.CreateStatus(
            dataSource,
            DataSourceEmbeddingState.RUNNING,
            totalFiles,
            0,
            inputFiles.FailedFiles,
            lastError: inputFiles.LastError,
            failures: inputFiles.Failures));

        var skippedFiles = 0;
        var permanentlySkippedFiles = 0;
        var completedFiles = 0;
        var newFiles = 0;
        var changedFiles = 0;
        var failedFiles = inputFiles.FailedFiles;
        var lastError = inputFiles.LastError;
        var failureDetails = inputFiles.Failures.ToList();

        //
        // Which kinds of provider failure the user was already told about in this run. A rejected
        // API key is the same problem for every one of a few thousand documents, and one message
        // is what it takes to send the user to the settings.
        //
        var reportedFailureReasons = new HashSet<ProviderRequestFailureReason>();

        //
        // Everything the runtime filters out of these files is reported once for the whole data
        // source. A run over a few thousand documents which removes something in forty of them
        // is one thing that happened to the user, not forty. The scope ends with this method, so
        // the report arrives when the run is finished rather than in the middle of it.
        //
        await using var promptInjectionReportingScope = guardService.BeginAction();

        foreach (var file in indexedFiles)
        {
            token.ThrowIfCancellationRequested();

            var fingerprint = metadataSnapshot.FileHashes[file.FullName];
            if (manifest.Files.TryGetValue(file.FullName, out var existingRecord) &&
                string.Equals(existingRecord.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                logger.LogDebug(
                    "Skipping unchanged file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) because the persisted metadata hash matches. MetadataHashPrefix={MetadataHashPrefix}, LastWriteUtc={LastWriteUtc:O}, FileSize={FileSize}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    ShortHash(fingerprint),
                    file.LastWriteTimeUtc,
                    file.Length);
                skippedFiles++;
                this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, lastError: lastError, failures: failureDetails, permanentlySkippedFiles: permanentlySkippedFiles));
                continue;
            }

            //
            // A file which failed for a reason of its own is not read again until it changes.
            // Without this, a folder holding hundreds of scanned documents without a text layer
            // would spend half an hour on every start to arrive at the result we already have:
            //
            if (manifest.PermanentFailures.TryGetValue(file.FullName, out var permanentFailure) &&
                string.Equals(permanentFailure.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                logger.LogDebug(
                    "Skipping file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) because reading it failed permanently before. FailureCode={FailureCode}, MetadataHashPrefix={MetadataHashPrefix}, OccurredAtUtc={OccurredAtUtc:O}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    permanentFailure.Code,
                    ShortHash(fingerprint),
                    permanentFailure.OccurredAtUtc);
                permanentlySkippedFiles++;

                // The stored reason keeps its place in the list, so the user still sees why:
                failureDetails.Add(new DataSourceEmbeddingFailure(file.FullName, permanentFailure.Message, permanentFailure.OccurredAtUtc, ExtractionCode: permanentFailure.Code, IsPermanent: true));
                this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, lastError: lastError, failures: failureDetails, permanentlySkippedFiles: permanentlySkippedFiles));
                continue;
            }

            this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, file.Name, lastError, failureDetails, permanentlySkippedFiles));

            //
            // What the page says while one file is being worked on. Without it, a document of
            // several thousand pages leaves the same sentence standing for hours, and a progress
            // which never moves cannot be told apart from one which is stuck.
            //
            var lastBlockReportUtc = DateTimeOffset.MinValue;

            try
            {
                logger.LogInformation(
                    "Embedding file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) because {EmbeddingReason}. CurrentMetadataHashPrefix={CurrentMetadataHashPrefix}. Progress={CompletedFiles}/{TotalFiles}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    GetFileEmbeddingReason(file, fingerprint, existingRecord),
                    ShortHash(fingerprint),
                    skippedFiles + completedFiles + 1,
                    totalFiles);
                var startedAtUtc = DateTimeOffset.UtcNow;
                var chunkCount = await context.IndexDocumentAsync(this.CreateFileDocument(context, dataSource, file, fingerprint), ReportBlockProgress, token);
                token.ThrowIfCancellationRequested();
                var fingerprintAfterEmbedding = BuildFileMetadataHash(file);
                if (!string.Equals(fingerprint, fingerprintAfterEmbedding, StringComparison.Ordinal))
                    throw new IOException(string.Format(TB("The file '{0}' changed while it was being indexed. What was indexed of it is discarded, and the file is tried again during the next run."), file.FullName));

                var embeddedAtUtc = DateTimeOffset.UtcNow;
                var record = new EmbeddedFileRecord(
                    fingerprint,
                    file.Length,
                    new DateTimeOffset(file.LastWriteTimeUtc),
                    embeddedAtUtc,
                    chunkCount);
                await context.IndexStore.UpsertFileAsync(
                    dataSource.Id,
                    this.CreateEmbeddingStateFile(dataSource, file, fingerprint, chunkCount, embeddedAtUtc),
                    token);
                manifest.Files[file.FullName] = record;
                await context.ForgetPermanentFailureAsync(file.FullName, token);
                completedFiles++;
                if (existingRecord is null)
                    newFiles++;
                else
                    changedFiles++;

                logger.LogInformation(
                    "Embedded file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) successfully. Chunks={ChunkCount}, DurationMs={DurationMs}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    chunkCount,
                    (DateTimeOffset.UtcNow - startedAtUtc).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (ProviderRequestException exception)
            {
                //
                // The provider said what went wrong and what the user can do about it. That
                // sentence is what goes into the status, together with the classification the UI
                // needs to offer the matching way out.
                //
                failedFiles++;
                lastError = exception.UserMessage;
                failureDetails.Add(new DataSourceEmbeddingFailure(file.FullName, exception.UserMessage, DateTimeOffset.UtcNow, exception.FailureReason, exception.StatusCode, context.EmbeddingProvider.Name));
                manifest.Files.Remove(file.FullName);
                await context.ForgetPermanentFailureAsync(file.FullName, token);
                await context.CleanupFailedDocumentAsync(file.FullName, token);

                logger.LogWarning(
                    exception,
                    "Failed to embed file '{FilePath}' for data source '{DataSourceName}' because the embedding provider '{EmbeddingProviderName}' failed. FailureReason={FailureReason}, StatusCode={StatusCode}.",
                    file.FullName,
                    dataSource.Name,
                    context.EmbeddingProvider.Name,
                    exception.FailureReason,
                    exception.StatusCode);
                this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, file.Name, exception.UserMessage, failureDetails));

                // Once per kind of failure, not once per file:
                if (reportedFailureReasons.Add(exception.FailureReason))
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, exception.UserMessage));
            }
            catch (FileExtractionException exception) when (exception.Code.IsPermanentIndexingFailure())
            {
                //
                // The file itself is why this failed, so trying it again changes nothing until the
                // file does. The reason is written into the index, and the fingerprint next to it
                // decides when to come back: an OCR run over a scanned PDF changes both size and
                // write time, which is exactly the moment the file deserves another attempt.
                //
                permanentlySkippedFiles++;
                var occurredAtUtc = DateTimeOffset.UtcNow;
                var indexingMessage = exception.Code.ToIndexingUserMessage(file.Name);
                failureDetails.Add(new DataSourceEmbeddingFailure(file.FullName, indexingMessage, occurredAtUtc, ExtractionCode: exception.Code, IsPermanent: true));
                manifest.Files.Remove(file.FullName);
                await context.CleanupFailedDocumentAsync(file.FullName, token);

                var absolutePath = Path.GetFullPath(file.FullName);
                manifest.PermanentFailures[absolutePath] = new PermanentIndexingFailureRecord(fingerprint, exception.Code, indexingMessage, occurredAtUtc);
                await context.IndexStore.UpsertPermanentFailureAsync(
                    dataSource.Id,
                    new PermanentIndexingFailure(IndexedDocumentIds.CreateParentId(dataSource.Id, absolutePath), absolutePath, fingerprint, exception.Code, indexingMessage, occurredAtUtc),
                    token);

                logger.LogInformation(
                    exception,
                    "Skipping file '{FilePath}' of data source '{DataSourceName}' ({DataSourceId}) from now on because reading it failed for a reason which lies in the file. FailureCode={FailureCode}, MetadataHashPrefix={MetadataHashPrefix}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    exception.Code,
                    ShortHash(fingerprint));
                this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, file.Name, lastError, failureDetails, permanentlySkippedFiles));
            }
            catch (VectorStoreUnreadableException)
            {
                //
                // Not about this one file: the store of the whole data source cannot be opened, so
                // every remaining file would fail the same way. Carrying on would fill the list
                // with one entry per file and hide the single cause behind them.
                //
                throw;
            }
            catch (Exception exception)
            {
                //
                // Everything which is not the provider's doing: a file which changed while it was
                // read, one which yielded no text, a vector store which refused to store. These
                // are about this one file, so they go into the list and not into a message which
                // would interrupt whatever the user is doing right now.
                //
                failedFiles++;
                var extractionCode = exception is FileExtractionException extractionFailure ? extractionFailure.Code : FileExtractionErrorCode.NONE;

                //
                // Deliberately not the message of the exception: that one is written for the log
                // file, in English, and repeats the path which the list shows anyway.
                //
                var failureMessage = extractionCode.ToIndexingUserMessage(file.Name);
                lastError = failureMessage;
                failureDetails.Add(new DataSourceEmbeddingFailure(file.FullName, failureMessage, DateTimeOffset.UtcNow, EmbeddingProviderName: context.EmbeddingProvider.Name, ExtractionCode: extractionCode));
                manifest.Files.Remove(file.FullName);
                await context.ForgetPermanentFailureAsync(file.FullName, token);
                await context.CleanupFailedDocumentAsync(file.FullName, token);

                logger.LogWarning(exception, "Failed to embed file '{FilePath}' for data source '{DataSourceName}'.", file.FullName, dataSource.Name);
                this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, file.Name, failureMessage, failureDetails, permanentlySkippedFiles));
            }

            continue;

            void ReportBlockProgress(int blockNumber, int? pageNumber)
            {
                //
                // The first block goes out at once, so the line is there instead of blank. After
                // that, at most one message every BLOCK_PROGRESS_INTERVAL: each one re-renders the
                // embedding page, the navigation bar and the table in the settings, and the blocks
                // of a large file arrive far faster than anybody can read them.
                //
                var nowUtc = DateTimeOffset.UtcNow;
                if (blockNumber > 1 && nowUtc - lastBlockReportUtc < BLOCK_PROGRESS_INTERVAL)
                    return;

                lastBlockReportUtc = nowUtc;
                this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, file.Name, lastError, failureDetails, permanentlySkippedFiles, blockNumber, pageNumber));
            }
        }

        manifest.SourceHash = metadataSnapshot.SourceHash;
        token.ThrowIfCancellationRequested();
        await context.OptimizeCollectionIfNeededAsync("data source embedding run finished", token);

        token.ThrowIfCancellationRequested();
        await context.IndexStore.UpdateDataSourceHashAsync(dataSource.Id, metadataSnapshot.SourceHash, token);
        token.ThrowIfCancellationRequested();

        this.UpsertStatus(this.CreateCompletedStatus(dataSource, totalFiles, skippedFiles + completedFiles, failedFiles, lastError, failureDetails, permanentlySkippedFiles));
        logger.LogInformation(
            "Finished background embeddings for data source '{DataSourceName}' ({DataSourceId}). RefreshMode={RefreshMode}, Embedded={EmbeddedFiles}, New={NewFiles}, Changed={ChangedFiles}, Skipped={SkippedFiles}, PermanentlySkipped={PermanentlySkippedFiles}, RemovedMissing={RemovedMissingFiles}, Failed={FailedFiles}, Total={TotalFiles}, SourceHashPrefix={SourceHashPrefix}.",
            dataSource.Name,
            dataSource.Id,
            refreshMode,
            completedFiles,
            newFiles,
            changedFiles,
            skippedFiles,
            permanentlySkippedFiles,
            removedMissingFiles,
            failedFiles,
            totalFiles,
            ShortHash(metadataSnapshot.SourceHash));
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

        var supportedDataSources = settingsManager.ConfigurationData.DataSources
            .Where(this.IsSupportedIndexedSource)
            .ToList();

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
                knownStatus?.TotalFiles ?? 0,
                knownStatus?.IndexedFiles ?? 0,
                knownStatus?.FailedFiles ?? 0,
                failures: knownStatus?.Failures ?? [],
                permanentlySkippedFiles: knownStatus?.PermanentlySkippedFiles ?? 0);
        }

        // One message for the whole list, rather than one per data source:
        this.PublishStatusChanged();

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

        return dataSource is DataSourceLocalDirectory or DataSourceLocalFile;
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
        var configuredDataSource = settingsManager.ConfigurationData.DataSources
            .FirstOrDefault(source => source.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase));

        dataSource = configuredDataSource is IIndexedDataSource indexedDataSource && this.IsSupportedIndexedSource(indexedDataSource)
            ? indexedDataSource
            : null;

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

    private async Task<int> RemoveMissingFileEmbeddingsAsync(IndexedRunContext context, IReadOnlyCollection<FileInfo> indexedFiles, CancellationToken token)
    {
        var manifest = context.Manifest;
        var existingPaths = indexedFiles
            .Select(file => file.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removedFiles = 0;
        foreach (var removedFilePath in manifest.Files.Keys.Except(existingPaths, StringComparer.OrdinalIgnoreCase).ToList())
        {
            await context.DeleteDocumentPointsAsync(removedFilePath, token);
            await context.IndexStore.DeleteFileAsync(context.DataSource.Id, removedFilePath, token);
            manifest.Files.Remove(removedFilePath);
            removedFiles++;
            logger.LogInformation(
                "Removed stale embeddings for deleted file '{FilePath}' from data source '{DataSourceName}' ({DataSourceId}).",
                removedFilePath,
                context.DataSource.Name,
                context.DataSource.Id);
        }

        //
        // A file which is gone needs no mark keeping it out of the index. Without this, the table
        // would grow with every document the user ever deleted:
        //
        foreach (var removedFilePath in manifest.PermanentFailures.Keys.Except(existingPaths, StringComparer.OrdinalIgnoreCase).ToList())
            await context.ForgetPermanentFailureAsync(removedFilePath, token);

        return removedFiles;
    }

    /// <remarks>
    /// A file counts as settled when it was indexed or when it was skipped for good, both with a
    /// matching fingerprint. Counting only the indexed ones would let a single unreadable document
    /// send the whole folder through the slow path on every run.
    /// </remarks>
    private bool CanSkipDataSourceByHash(DataSourceEmbeddingManifest manifest, DataSourceMetadataSnapshot metadataSnapshot, IReadOnlyCollection<FileInfo> indexedFiles)
    {
        if (!string.Equals(manifest.SourceHash, metadataSnapshot.SourceHash, StringComparison.Ordinal))
            return false;

        if (manifest.Files.Count + manifest.PermanentFailures.Count != indexedFiles.Count)
            return false;

        foreach (var file in indexedFiles)
        {
            if (!metadataSnapshot.FileHashes.TryGetValue(file.FullName, out var currentHash))
                return false;

            if (manifest.Files.TryGetValue(file.FullName, out var existingRecord))
            {
                if (!string.Equals(existingRecord.Fingerprint, currentHash, StringComparison.Ordinal))
                    return false;

                continue;
            }

            if (!manifest.PermanentFailures.TryGetValue(file.FullName, out var permanentFailure))
                return false;

            if (!string.Equals(permanentFailure.Fingerprint, currentHash, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static List<DataSourceEmbeddingFailure> CreatePermanentFailureDetails(DataSourceEmbeddingManifest manifest) => manifest.PermanentFailures
        .Select(failure => new DataSourceEmbeddingFailure(failure.Key, failure.Value.Message, failure.Value.OccurredAtUtc, ExtractionCode: failure.Value.Code, IsPermanent: true))
        .ToList();

    private static string GetFileEmbeddingReason(FileInfo file, string currentHash, EmbeddedFileRecord? existingRecord)
    {
        if (existingRecord is null)
            return "no stored file hash exists";

        var reasons = new List<string>();
        if (!string.Equals(existingRecord.Fingerprint, currentHash, StringComparison.Ordinal))
            reasons.Add($"stored hash {ShortHash(existingRecord.Fingerprint)} differs from current hash {ShortHash(currentHash)}");

        if (existingRecord.FileSize != file.Length)
            reasons.Add($"file size changed from {existingRecord.FileSize} to {file.Length} bytes");

        if (existingRecord.LastWriteUtc != new DateTimeOffset(file.LastWriteTimeUtc))
            reasons.Add($"last modified time changed from {existingRecord.LastWriteUtc:O} to {file.LastWriteTimeUtc:O}");

        return reasons.Count == 0
            ? "the file hash changed"
            : string.Join("; ", reasons);
    }

    private static string ShortHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        return value.Length <= 12 ? value : value[..12];
    }

    private DataSourceEmbeddingStatus CreateStatus(
        IDataSourceBase dataSource,
        DataSourceEmbeddingState state,
        int totalFiles,
        int indexedFiles,
        int failedFiles,
        string currentFile = "",
        string lastError = "",
        IReadOnlyList<DataSourceEmbeddingFailure>? failures = null,
        int permanentlySkippedFiles = 0,
        int? currentFileBlock = null,
        int? currentFilePage = null,
        bool vectorStoreUnreadable = false)
    {
        return new DataSourceEmbeddingStatus(
            dataSource.Id,
            dataSource.Name,
            dataSource.Type,
            state,
            totalFiles,
            indexedFiles,
            failedFiles,
            currentFile,
            lastError,
            failures?.ToList() ?? [],
            permanentlySkippedFiles,
            currentFileBlock,
            currentFilePage,
            vectorStoreUnreadable);
    }

    /// <remarks>
    /// Files which were skipped for good do not make a run unsuccessful: nothing is left to try,
    /// and a data source made of nothing but scanned images would otherwise ask for attention
    /// forever.
    /// </remarks>
    private DataSourceEmbeddingStatus CreateCompletedStatus(IDataSourceBase dataSource, int totalFiles, int indexedFiles, int failedFiles, string lastError, IReadOnlyList<DataSourceEmbeddingFailure>? failures = null, int permanentlySkippedFiles = 0)
    {
        return this.CreateStatus(
            dataSource,
            failedFiles > 0 ? DataSourceEmbeddingState.FAILED : DataSourceEmbeddingState.COMPLETED,
            totalFiles,
            indexedFiles,
            failedFiles,
            lastError: failedFiles > 0
                ? string.IsNullOrWhiteSpace(lastError)
                    ? TB("Some files could not be indexed. The list below says which ones and why.")
                    : lastError
                : string.Empty,
            failures: failures,
            permanentlySkippedFiles: permanentlySkippedFiles);
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

    private DataSourceQueueRequestResult TryReserveDataSourceQueueSlot(string dataSourceId, bool queueAfterCurrentRun)
    {
        lock (this.queueStateLock)
        {
            if (this.runningIds.ContainsKey(dataSourceId))
            {
                if (queueAfterCurrentRun && this.pendingQueueIds.TryAdd(dataSourceId, 0))
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

    private bool TryCompleteDataSourceRun(string dataSourceId, bool allowPendingRequeue)
    {
        lock (this.queueStateLock)
        {
            this.runningIds.TryRemove(dataSourceId, out _);

            if (!this.pendingQueueIds.TryRemove(dataSourceId, out _))
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
            this.pendingQueueIds.TryRemove(dataSourceId, out _);
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
        if (!this.TryCompleteDataSourceRun(dataSourceId, isConfigured))
            return;

        if (dataSource is null)
        {
            this.ReleaseQueuedDataSourceRun(dataSourceId);
            return;
        }

        logger.LogInformation("Queueing one follow-up embedding run for data source '{DataSourceName}' ({DataSourceId}) after changes arrived during the previous run.", dataSource.Name, dataSource.Id);

        this.statuses.TryGetValue(dataSource.Id, out var currentStatus);
        this.UpsertStatus(this.CreateStatus(
            dataSource,
            DataSourceEmbeddingState.QUEUED,
            currentStatus?.TotalFiles ?? 0,
            currentStatus?.IndexedFiles ?? 0,
            currentStatus?.FailedFiles ?? 0,
            lastError: currentStatus?.LastError ?? string.Empty,
            failures: currentStatus?.Failures ?? []));

        try
        {
            await this.queue.Writer.WriteAsync(new DataSourceEmbeddingQueueItem(dataSourceId, DataSourceEmbeddingRefreshMode.HASH_CHECK), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            this.ReleaseQueuedDataSourceRun(dataSourceId);
        }
    }

    private void UpsertStatus(DataSourceEmbeddingStatus status)
    {
        this.statuses[status.DataSourceId] = status;
        this.PublishStatusChanged();
    }

    private void PublishStatusChanged()
    {
        _ = MessageBus.INSTANCE.SendMessage(null, Event.RAG_EMBEDDING_STATUS_CHANGED, true);
    }
}
