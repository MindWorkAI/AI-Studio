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

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService(SettingsManager settingsManager, RustService rustService, DatabaseClientProvider databaseClientProvider,
    PromptInjectionGuardService guardService, ILogger<DataSourceEmbeddingService> logger) : BackgroundService
{
    private const int VECTOR_STORE_OPTIMIZATION_CHUNK_THRESHOLD = 100_000;

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

    private sealed class VectorStoreOptimizationTracker
    {
        public long StoredChunksSinceLastOptimization { get; private set; }

        public bool HasPendingChanges { get; private set; }

        public void MarkChanged()
        {
            this.HasPendingChanges = true;
        }

        public void RecordStoredChunks(int chunkCount)
        {
            if (chunkCount <= 0)
                return;

            this.HasPendingChanges = true;
            this.StoredChunksSinceLastOptimization += chunkCount;
        }

        public void Reset()
        {
            this.StoredChunksSinceLastOptimization = 0;
            this.HasPendingChanges = false;
        }
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
            var total = Math.Max(activeStatus.TotalFiles, 1);
            return new(
                true,
                activeStatus.State,
                activeStatus.IndexedFiles,
                total,
                activeStatus.FailedFiles);
        }

        var failedStatus = orderedStatuses
            .FirstOrDefault(status => status.State is DataSourceEmbeddingState.FAILED || status.FailedFiles > 0);

        if (failedStatus is not null)
            return new(true, DataSourceEmbeddingState.FAILED, failedStatus.IndexedFiles, failedStatus.TotalFiles, failedStatus.FailedFiles);

        return new(false,  DataSourceEmbeddingState.COMPLETED, 0, 0, 0);
    }

    public Task QueueAllInternalDataSourcesAsync()
    {
        return this.QueueAllInternalDataSourcesAsync(true);
    }

    private Task QueueAllInternalDataSourcesAsync(bool queueAfterCurrentRun)
    {
        this.RefreshWatchers();

        var supportedDataSources = settingsManager.ConfigurationData.DataSources
            .Where(this.IsSupportedInternalDataSource)
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

    public bool CanRefreshDataSource(IDataSource dataSource)
    {
        return this.IsSupportedInternalDataSource(dataSource);
    }

    public bool CanRefreshDataSource(string dataSourceId)
    {
        return this.TryGetConfiguredDataSource(dataSourceId, out var dataSource) &&
            this.CanRefreshDataSource(dataSource);
    }

    public async Task<bool> ShouldLockDataSourceIdentityAsync(string dataSourceId, CancellationToken token = default)
    {
        var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
        if (!indexStore.IsAvailable)
        {
            logger.LogWarning("Locking identity settings for data source '{DataSourceId}' because the local RAG index database '{DatabaseName}' is unavailable.", dataSourceId, indexStore.Name);
            return true;
        }

        var manifest = await indexStore.GetManifestAsync(dataSourceId, token);
        return !string.IsNullOrWhiteSpace(manifest.EmbeddingProviderId)
               || !string.IsNullOrWhiteSpace(manifest.EmbeddingSignature)
               || !string.IsNullOrWhiteSpace(manifest.SourceHash)
               || manifest.VectorSize > 0
               || manifest.Files.Count > 0

               // A data source whose files were all skipped for good has index state as well,
               // even though nothing was indexed of it:
               || manifest.PermanentFailures.Count > 0;
    }

    public Task QueueDataSourceAsync(IDataSource dataSource)
    {
        return this.QueueDataSourceAsync(dataSource, true, DataSourceEmbeddingRefreshMode.HASH_CHECK);
    }

    public Task QueueDataSourceAsync(string dataSourceId)
    {
        return this.TryGetConfiguredDataSource(dataSourceId, out var dataSource)
            ? this.QueueDataSourceAsync(dataSource)
            : Task.CompletedTask;
    }

    public Task RetryDataSourceAsync(string dataSourceId)
    {
        return this.TryGetConfiguredDataSource(dataSourceId, out var dataSource)
            ? this.QueueDataSourceAsync(dataSource, true, DataSourceEmbeddingRefreshMode.MANUAL_RETRY)
            : Task.CompletedTask;
    }

    private async Task QueueDataSourceAsync(IDataSource dataSource, bool queueAfterCurrentRun, DataSourceEmbeddingRefreshMode refreshMode)
    {
        if (!this.IsSupportedInternalDataSource(dataSource))
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

    public async Task RemoveDataSourceAsync(IDataSource dataSource)
    {
        if (!this.IsSupportedInternalDataSource(dataSource))
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

            IDataSource? dataSource = null;

            try
            {
                dataSource = settingsManager.ConfigurationData.DataSources
                    .FirstOrDefault(source => source.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase));

                if (dataSource is null || !this.IsSupportedInternalDataSource(dataSource))
                    continue;

                await this.ProcessDataSourceRunAsync(dataSource, queueItem.RefreshMode, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
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
                    this.UpsertStatus(this.GetFallbackStatus(dataSource, exception.Message));
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

    private async Task ProcessDataSourceRunAsync(IDataSource dataSource, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken parentToken)
    {
        if (!this.TryGetConfiguredDataSource(dataSource.Id, out var configuredDataSource) ||
            !this.IsSupportedInternalDataSource(configuredDataSource))
        {
            logger.LogDebug(
                "Skipping embedding run for data source '{DataSourceName}' ({DataSourceId}) because it is no longer configured. RefreshMode={RefreshMode}.",
                dataSource.Name,
                dataSource.Id,
                refreshMode);
            return;
        }

        dataSource = configuredDataSource;
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

    private async Task ProcessDataSourceAsync(IDataSource dataSource, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        if (dataSource is not IInternalDataSource internalDataSource)
        {
            logger.LogWarning(
                "Skipping background embeddings for non-internal data source '{DataSourceName}' ({DataSourceId}).",
                dataSource.Name,
                dataSource.Id);
            return;
        }

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
            this.UpsertStatus(this.GetFallbackStatus(dataSource, "The vector database is not available."));
            return;
        }

        if (!indexStore.IsAvailable)
        {
            logger.LogWarning(
                "Skipping background embeddings for data source '{DataSourceName}' ({DataSourceId}) because the database client '{DatabaseName}' is unavailable.",
                dataSource.Name,
                dataSource.Id,
                indexStore.Name);
            token.ThrowIfCancellationRequested();
            this.UpsertStatus(this.GetFallbackStatus(dataSource, "The local RAG index database is not available."));
            return;
        }

        var collectionName = DataSourceEmbeddingNames.GetCollectionName(dataSource.Id);
        var persistedManifest = await indexStore.GetManifestAsync(dataSource.Id, token);
        if (persistedManifest.VectorSize > 0)
        {
            var ensureResult = await vectorStore.EnsureVectorStoreExists(collectionName, dataSource.Name, persistedManifest.VectorSize, token);
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
            return;
        }

        if (!embeddingProvider.GetConfidenceLevel(settingsManager).AllowsDataSourceConfidenceLevel(internalDataSource.ConfidenceLevel))
        {
            var errorMessage = string.Format(TB("The selected embedding provider is not allowed to index this data source. The data source asks for the confidence level '{0}', while the embedding provider has '{1}'."), internalDataSource.ConfidenceLevel.GetName(), embeddingProvider.GetConfidenceLevel(settingsManager).GetName());
            logger.LogWarning(
                "Skipping background embeddings for data source '{DataSourceName}' ({DataSourceId}) because embedding provider '{EmbeddingProviderName}' ({EmbeddingProviderId}) does not meet the required confidence. RequiredConfidence={RequiredConfidence}, EmbeddingProviderConfidence={EmbeddingProviderConfidence}.",
                dataSource.Name,
                dataSource.Id,
                embeddingProvider.Name,
                embeddingProvider.Id,
                internalDataSource.ConfidenceLevel.GetName(),
                embeddingProvider.GetConfidenceLevel(settingsManager).GetName());

            token.ThrowIfCancellationRequested();
            this.UpsertStatus(this.GetFallbackStatus(dataSource, errorMessage));
            return;
        }

        logger.LogInformation(
            "Using embedding provider '{EmbeddingProviderId}' with model '{EmbeddingModelId}' for data source '{DataSourceName}' ({DataSourceId}).",
            embeddingProvider.Id,
            embeddingProvider.Model.Id,
            dataSource.Name,
            dataSource.Id);

        var manifest = await this.EnsureCompatibleManifestAsync(dataSource, embeddingProvider, collectionName, vectorStore, indexStore, token);
        token.ThrowIfCancellationRequested();

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
            collectionName);

        var metadataSnapshot = this.BuildDataSourceMetadataSnapshot(dataSource, indexedFiles);
        var removedMissingFiles = await this.RemoveMissingFileEmbeddingsAsync(vectorStore, indexStore, dataSource, collectionName, manifest, indexedFiles, token);
        var optimizationTracker = new VectorStoreOptimizationTracker();
        if (removedMissingFiles > 0)
            optimizationTracker.MarkChanged();
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

            await this.OptimizeCollectionIfNeededAsync(
                optimizationTracker,
                vectorStore,
                collectionName,
                dataSource,
                "data source finished after removing missing files",
                token);

            token.ThrowIfCancellationRequested();
            await indexStore.UpdateDataSourceHashAsync(dataSource.Id, metadataSnapshot.SourceHash, token);

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

        var provider = embeddingProvider.CreateProvider();
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
                var chunkCount = await this.IndexOneFileAsync(indexStore, vectorStore, dataSource, file, fingerprint, embeddingProvider, provider, manifest, optimizationTracker, token);
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
                await indexStore.UpsertFileAsync(
                    dataSource.Id,
                    this.CreateEmbeddingStateFile(dataSource, file, fingerprint, chunkCount, embeddedAtUtc),
                    token);
                manifest.Files[file.FullName] = record;
                await this.ForgetPermanentFailureAsync(indexStore, dataSource, manifest, file.FullName, token);
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
                failureDetails.Add(new DataSourceEmbeddingFailure(file.FullName, exception.UserMessage, DateTimeOffset.UtcNow, exception.FailureReason, exception.StatusCode, embeddingProvider.Name));
                manifest.Files.Remove(file.FullName);
                await this.ForgetPermanentFailureAsync(indexStore, dataSource, manifest, file.FullName, token);
                await this.CleanupFailedFileAsync(indexStore, vectorStore, dataSource, collectionName, file.FullName, optimizationTracker, token);

                logger.LogWarning(
                    exception,
                    "Failed to embed file '{FilePath}' for data source '{DataSourceName}' because the embedding provider '{EmbeddingProviderName}' failed. FailureReason={FailureReason}, StatusCode={StatusCode}.",
                    file.FullName,
                    dataSource.Name,
                    embeddingProvider.Name,
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
                await this.CleanupFailedFileAsync(indexStore, vectorStore, dataSource, collectionName, file.FullName, optimizationTracker, token);

                var absolutePath = Path.GetFullPath(file.FullName);
                manifest.PermanentFailures[absolutePath] = new PermanentIndexingFailureRecord(fingerprint, exception.Code, indexingMessage, occurredAtUtc);
                await indexStore.UpsertPermanentFailureAsync(
                    dataSource.Id,
                    new PermanentIndexingFailure(this.CreateParentFileId(dataSource.Id, absolutePath), absolutePath, fingerprint, exception.Code, indexingMessage, occurredAtUtc),
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
            catch (Exception exception)
            {
                //
                // Everything which is not the provider's doing: a file which changed while it was
                // read, one which yielded no text, a vector store which refused to store. These
                // are about this one file, so they go into the list and not into a message which
                // would interrupt whatever the user is doing right now.
                //
                failedFiles++;
                lastError = exception.Message;
                failureDetails.Add(new DataSourceEmbeddingFailure(file.FullName, exception.Message, DateTimeOffset.UtcNow, EmbeddingProviderName: embeddingProvider.Name, ExtractionCode: exception is FileExtractionException extractionFailure ? extractionFailure.Code : FileExtractionErrorCode.NONE));
                manifest.Files.Remove(file.FullName);
                await this.ForgetPermanentFailureAsync(indexStore, dataSource, manifest, file.FullName, token);
                await this.CleanupFailedFileAsync(indexStore, vectorStore, dataSource, collectionName, file.FullName, optimizationTracker, token);

                logger.LogWarning(exception, "Failed to embed file '{FilePath}' for data source '{DataSourceName}'.", file.FullName, dataSource.Name);
                this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, file.Name, exception.Message, failureDetails, permanentlySkippedFiles));
            }
        }

        manifest.SourceHash = metadataSnapshot.SourceHash;
        token.ThrowIfCancellationRequested();
        await this.OptimizeCollectionIfNeededAsync(
            optimizationTracker,
            vectorStore,
            collectionName,
            dataSource,
            "data source embedding run finished",
            token);

        token.ThrowIfCancellationRequested();
        await indexStore.UpdateDataSourceHashAsync(dataSource.Id, metadataSnapshot.SourceHash, token);
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

    private async Task<int> IndexOneFileAsync(
        IndexStoreClient indexStore,
        VectorStoreClient vectorStore,
        IDataSource dataSource,
        FileInfo file,
        string fingerprint,
        EmbeddingProvider embeddingProvider,
        IProvider provider,
        DataSourceEmbeddingManifest manifest,
        VectorStoreOptimizationTracker optimizationTracker,
        CancellationToken token)
    {
        var collectionName = DataSourceEmbeddingNames.GetCollectionName(dataSource.Id);
        logger.LogDebug(
            "Resetting stored embeddings for file '{FilePath}' in collection '{CollectionName}' before re-indexing.",
            file.FullName,
            collectionName);
        await this.DeleteFilePointsAsync(vectorStore, collectionName, file.FullName, token);
        optimizationTracker.MarkChanged();
        await indexStore.DeleteFileAsync(dataSource.Id, file.FullName, token);

        var parentFile = this.CreateEmbeddingStateFile(dataSource, file, fingerprint, 0, DateTimeOffset.UtcNow);
        await indexStore.UpsertFileAsync(dataSource.Id, parentFile, token);

        var embeddingBatchSize = Math.Max(1, embeddingProvider.EffectiveEmbeddingBatchSize);
        var batch = new List<EmbeddingChunkDraft>(embeddingBatchSize);
        var totalChunkCount = 0;

        await foreach (var chunk in this.StreamEmbeddingChunksAsync(file.FullName, dataSource, embeddingProvider, token))
        {
            batch.Add(new(this.CreatePointId(dataSource.Id, fingerprint, totalChunkCount), chunk, totalChunkCount, TryExtractPageNumber(chunk)));
            totalChunkCount++;

            if (batch.Count >= embeddingBatchSize)
                await this.FlushBatchAsync(indexStore, vectorStore, dataSource, file, fingerprint, parentFile, embeddingProvider, provider, manifest, optimizationTracker, collectionName, batch, token);
        }

        if (batch.Count > 0)
            await this.FlushBatchAsync(indexStore, vectorStore, dataSource, file, fingerprint, parentFile, embeddingProvider, provider, manifest, optimizationTracker, collectionName, batch, token);

        //
        // The extraction itself did not report a failure, but nothing usable came out of it. For
        // the index this is the same case as a scanned page without a text layer, which is why it
        // carries a code of its own instead of an unclassified exception:
        //
        if (totalChunkCount == 0)
            throw new FileExtractionException(FileExtractionErrorCode.NO_CONTENT, string.Format(TB("No text could be read from the file '{0}'."), file.Name));

        logger.LogDebug(
            "Generated {ChunkCount} chunks for file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
            totalChunkCount,
            file.FullName,
            dataSource.Name,
            dataSource.Id);

        return totalChunkCount;
    }

    private async Task FlushBatchAsync(
        IndexStoreClient indexStore,
        VectorStoreClient vectorStore,
        IDataSource dataSource,
        FileInfo file,
        string fingerprint,
        EmbeddingStateFile parentFile,
        EmbeddingProvider embeddingProvider,
        IProvider provider,
        DataSourceEmbeddingManifest manifest,
        VectorStoreOptimizationTracker optimizationTracker,
        string collectionName,
        List<EmbeddingChunkDraft> batch,
        CancellationToken token)
    {
        logger.LogDebug(
            "Requesting embeddings for batch of {ChunkCount} chunks from file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
            batch.Count,
            file.FullName,
            dataSource.Name,
            dataSource.Id);

        var texts = batch.Select(item => item.Text).ToList();
        IReadOnlyList<IReadOnlyList<float>> vectors;
        try
        {
            vectors = await provider.EmbedTextAsync(embeddingProvider.Model, settingsManager, token, texts);
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (ProviderRequestException)
        {
            //
            // The provider already named the cause and what to do about it. Wrapping that in a
            // sentence about a batch of chunks would replace the one thing the user can act on
            // with the fact that something failed:
            //
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(string.Format(TB("The embedding provider was not able to embed {0} part(s) of the file '{1}'. The provider reported: {2}"), batch.Count, file.Name, exception.Message), exception);
        }

        if (vectors.Count != batch.Count)
            throw new InvalidOperationException(string.Format(TB("The embedding provider answered with {0} vectors for {1} parts of the file '{2}'. Please select another embedding model or provider."), vectors.Count, batch.Count, file.Name));

        var vectorSize = vectors.FirstOrDefault()?.Count ?? 0;
        if (vectorSize <= 0)
            throw new InvalidOperationException(TB("The embedding provider answered with an empty vector. Please select another embedding model or provider."));

        if (vectors.Any(vector => vector.Count != vectorSize))
            throw new InvalidOperationException(TB("The embedding provider answered with vectors of different sizes. Please select another embedding model or provider."));

        if (vectors.Any(vector => vector.Any(value => !float.IsFinite(value))))
            throw new InvalidOperationException(TB("The embedding provider answered with a vector containing an invalid number. Please select another embedding model or provider."));

        if (manifest.VectorSize > 0 && manifest.VectorSize != vectorSize)
            throw new InvalidOperationException(string.Format(TB("The size of the embedding vectors changed from {0} to {1}. Please save the data source again to index it from scratch."), manifest.VectorSize, vectorSize));

        if (manifest.VectorSize == 0)
        {
            token.ThrowIfCancellationRequested();
            var ensureResult = await vectorStore.EnsureVectorStoreExists(collectionName, dataSource.Name, vectorSize, token);
            if (!ensureResult.Created)
            {
                logger.LogWarning(
                    "Vector store '{CollectionName}' exists for data source '{DataSourceName}' ({DataSourceId}) although no persisted embedding state exists. Replacing the orphaned store before indexing.",
                    collectionName,
                    dataSource.Name,
                    dataSource.Id);
                await vectorStore.DeleteVectorStore(collectionName, token);
                ensureResult = await vectorStore.EnsureVectorStoreExists(collectionName, dataSource.Name, vectorSize, token);
                if (!ensureResult.Created)
                    throw new InvalidOperationException(string.Format(TB("The local index '{0}' could not be created again. Please restart AI Studio and try once more."), collectionName));
            }

            await indexStore.UpdateVectorSizeAsync(dataSource.Id, vectorSize, token);
            manifest.VectorSize = vectorSize;
            logger.LogInformation(
                "Created embedding collection '{CollectionName}' with vector size {VectorSize} for data source '{DataSourceName}' ({DataSourceId}).",
                collectionName,
                vectorSize,
                dataSource.Name,
                dataSource.Id);
        }

        token.ThrowIfCancellationRequested();
        var embeddedAtUtc = DateTimeOffset.UtcNow;
        await this.UpsertPointsAsync(
            vectorStore,
            collectionName,
            dataSource,
            file,
            fingerprint,
            parentFile,
            batch,
            vectors,
            embeddedAtUtc,
            token);
        token.ThrowIfCancellationRequested();
        await indexStore.UpsertChunksAsync(
            dataSource.Id,
            this.CreateEmbeddingStateChunks(parentFile, batch, embeddedAtUtc),
            token);

        optimizationTracker.RecordStoredChunks(batch.Count);
        if (optimizationTracker.StoredChunksSinceLastOptimization >= VECTOR_STORE_OPTIMIZATION_CHUNK_THRESHOLD)
            await this.OptimizeCollectionIfNeededAsync(
                optimizationTracker,
                vectorStore,
                collectionName,
                dataSource,
                "stored chunk threshold reached",
                token);

        logger.LogDebug(
            "Stored {ChunkCount} embedded chunks for file '{FilePath}' in collection '{CollectionName}'.",
            batch.Count,
            file.FullName,
            collectionName);

        batch.Clear();
    }

    private async Task UpsertPointsAsync(
        VectorStoreClient vectorStore,
        string collectionName,
        IDataSource dataSource,
        FileInfo file,
        string fingerprint,
        EmbeddingStateFile parentFile,
        IReadOnlyList<EmbeddingChunkDraft> batch,
        IReadOnlyList<IReadOnlyList<float>> vectors,
        DateTimeOffset embeddedAtUtc,
        CancellationToken token)
    {
        var points = batch.Select((item, index) => new VectorStoragePoint(
            item.ChunkId,
            vectors[index],
            dataSource.Id,
            dataSource.Name,
            dataSource.Type.ToString(),
            item.ChunkId,
            parentFile.ParentFileId,
            file.FullName,
            parentFile.AbsolutePath,
            parentFile.FileName,
            parentFile.RelativePath,
            parentFile.FileType,
            item.PageNumber,
            item.ChunkIndex,
            item.Text,
            fingerprint,
            parentFile.CreationUtc,
            parentFile.LastWriteUtc,
            embeddedAtUtc,
            parentFile.ConfidenceLevel,
            parentFile.ConfidenceLevelRank)).ToList();

        await vectorStore.InsertEmbedding(collectionName, points, token);
    }

    private async Task DeleteFilePointsAsync(VectorStoreClient vectorStore, string collectionName, string filePath, CancellationToken token)
    {
        await vectorStore.DeleteEmbeddingByFile(collectionName, filePath, token);
    }

    private async Task CleanupFailedFileAsync(
        IndexStoreClient indexStore,
        VectorStoreClient vectorStore,
        IDataSource dataSource,
        string collectionName,
        string filePath,
        VectorStoreOptimizationTracker optimizationTracker,
        CancellationToken token)
    {
        try
        {
            await this.DeleteFilePointsAsync(vectorStore, collectionName, filePath, token);
            optimizationTracker.MarkChanged();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not remove vector points while cleaning up failed embedding for file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
                filePath,
                dataSource.Name,
                dataSource.Id);
        }

        try
        {
            await indexStore.DeleteFileAsync(dataSource.Id, filePath, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not remove embedding state while cleaning up failed embedding for file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
                filePath,
                dataSource.Name,
                dataSource.Id);
        }
    }

    private async Task OptimizeCollectionIfNeededAsync(
        VectorStoreOptimizationTracker optimizationTracker,
        VectorStoreClient vectorStore,
        string collectionName,
        IDataSource dataSource,
        string reason,
        CancellationToken token)
    {
        if (!optimizationTracker.HasPendingChanges)
            return;

        logger.LogInformation(
            "Optimizing embedding collection '{CollectionName}' for data source '{DataSourceName}' ({DataSourceId}). Reason='{Reason}', StoredChunksSinceLastOptimization={StoredChunksSinceLastOptimization}, ChunkThreshold={ChunkThreshold}.",
            collectionName,
            dataSource.Name,
            dataSource.Id,
            reason,
            optimizationTracker.StoredChunksSinceLastOptimization,
            VECTOR_STORE_OPTIMIZATION_CHUNK_THRESHOLD);

        await vectorStore.OptimizeVectorStore(collectionName, token);
        optimizationTracker.Reset();
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
            .Where(this.IsSupportedInternalDataSource)
            .ToList();

        logger.LogInformation(
            "Starting initial persisted hash check for {DataSourceCount} supported internal data source(s). Incomplete or failed local RAG embedding state will be retried during this pass. File watchers will be activated after this check completes.",
            supportedDataSources.Count);

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
            catch (Exception exception)
            {
                logger.LogError(exception, "Initial embedding hash check failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
                this.UpsertStatus(this.GetFallbackStatus(dataSource, exception.Message));
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

    private bool IsSupportedInternalDataSource(IDataSource dataSource)
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

    private bool TryGetConfiguredDataSource(string dataSourceId, [NotNullWhen(true)] out IDataSource? dataSource)
    {
        dataSource = settingsManager.ConfigurationData.DataSources
            .FirstOrDefault(source => source.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase));

        return dataSource is not null;
    }

    private bool TryResolveEmbeddingProvider(IDataSource dataSource, [NotNullWhen(true)] out EmbeddingProvider? embeddingProvider)
        => DataSourceEmbeddingProviders.TryResolve(settingsManager, dataSource, out embeddingProvider);

    private async Task<DataSourceEmbeddingManifest> EnsureCompatibleManifestAsync(
        IDataSource dataSource,
        EmbeddingProvider embeddingProvider,
        string collectionName,
        VectorStoreClient vectorStore,
        IndexStoreClient indexStore,
        CancellationToken token)
    {
        var chunkingOptions = this.GetChunkingOptions(dataSource, embeddingProvider);
        var embeddingSignature = this.BuildEmbeddingSignature(dataSource, embeddingProvider, chunkingOptions);
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
            dataSource.Name,
            dataSource.Type.ToString(),
            manifest.EmbeddingProviderId,
            manifest.EmbeddingSignature,
            manifest.SourceHash,
            manifest.VectorSize,
            token);

        return manifest;
    }

    private async Task<int> RemoveMissingFileEmbeddingsAsync(
        VectorStoreClient vectorStore,
        IndexStoreClient indexStore,
        IDataSource dataSource,
        string collectionName,
        DataSourceEmbeddingManifest manifest,
        IReadOnlyCollection<FileInfo> indexedFiles,
        CancellationToken token)
    {
        var existingPaths = indexedFiles
            .Select(file => file.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removedFiles = 0;
        foreach (var removedFilePath in manifest.Files.Keys.Except(existingPaths, StringComparer.OrdinalIgnoreCase).ToList())
        {
            await this.DeleteFilePointsAsync(vectorStore, collectionName, removedFilePath, token);
            await indexStore.DeleteFileAsync(dataSource.Id, removedFilePath, token);
            manifest.Files.Remove(removedFilePath);
            removedFiles++;
            logger.LogInformation(
                "Removed stale embeddings for deleted file '{FilePath}' from data source '{DataSourceName}' ({DataSourceId}).",
                removedFilePath,
                dataSource.Name,
                dataSource.Id);
        }

        //
        // A file which is gone needs no mark keeping it out of the index. Without this, the table
        // would grow with every document the user ever deleted:
        //
        foreach (var removedFilePath in manifest.PermanentFailures.Keys.Except(existingPaths, StringComparer.OrdinalIgnoreCase).ToList())
            await this.ForgetPermanentFailureAsync(indexStore, dataSource, manifest, removedFilePath, token);

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

    /// <summary>
    /// Drops the mark which keeps a file out of the index, in the store as well as in the manifest.
    /// </summary>
    /// <remarks>
    /// Called whenever a file was read, and whenever it failed for a reason outside of itself. The
    /// state heals on its own that way: a document which becomes readable, or a drive which comes
    /// back, leaves nothing behind.
    /// </remarks>
    private async Task ForgetPermanentFailureAsync(IndexStoreClient indexStore, IDataSource dataSource, DataSourceEmbeddingManifest manifest, string filePath, CancellationToken token)
    {
        if (!manifest.PermanentFailures.Remove(filePath))
            return;

        await indexStore.DeletePermanentFailureAsync(dataSource.Id, filePath, token);
        logger.LogDebug(
            "Removed the permanent indexing failure of file '{FilePath}' from data source '{DataSourceName}' ({DataSourceId}).",
            filePath,
            dataSource.Name,
            dataSource.Id);
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
        IDataSource dataSource,
        DataSourceEmbeddingState state,
        int totalFiles,
        int indexedFiles,
        int failedFiles,
        string currentFile = "",
        string lastError = "",
        IReadOnlyList<DataSourceEmbeddingFailure>? failures = null,
        int permanentlySkippedFiles = 0)
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
            permanentlySkippedFiles);
    }

    /// <remarks>
    /// Files which were skipped for good do not make a run unsuccessful: nothing is left to try,
    /// and a data source made of nothing but scanned images would otherwise ask for attention
    /// forever.
    /// </remarks>
    private DataSourceEmbeddingStatus CreateCompletedStatus(IDataSource dataSource, int totalFiles, int indexedFiles, int failedFiles, string lastError, IReadOnlyList<DataSourceEmbeddingFailure>? failures = null, int permanentlySkippedFiles = 0)
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

    private DataSourceEmbeddingStatus GetFallbackStatus(IDataSource dataSource, string errorMessage)
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

    private DataSourceRunControl? CancelActiveDataSourceRun(IDataSource dataSource)
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
        var dataSource = token.IsCancellationRequested
            ? null
            : settingsManager.ConfigurationData.DataSources
                .FirstOrDefault(source => source.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase));

        if (!this.TryCompleteDataSourceRun(dataSourceId, dataSource is not null && this.IsSupportedInternalDataSource(dataSource)))
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
