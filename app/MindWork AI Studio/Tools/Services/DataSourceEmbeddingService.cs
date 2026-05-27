using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService(SettingsManager settingsManager, RustService rustService, DatabaseClientProvider databaseClientProvider, ILogger<DataSourceEmbeddingService> logger)
    : BackgroundService
{
    private const int MAX_CHUNK_LENGTH = 3_200;
    private const int MIN_CHUNK_LENGTH = 800;
    private const int CHUNK_OVERLAP_LENGTH = 320;
    private const int EMBEDDING_BATCH_SIZE = 16;

    private readonly Channel<string> queue = Channel.CreateUnbounded<string>();
    private readonly ConcurrentDictionary<string, byte> queuedIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DataSourceEmbeddingStatus> statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim stateLock = new(1, 1);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceEmbeddingService).Namespace, nameof(DataSourceEmbeddingService));

    private Dictionary<string, DataSourceEmbeddingManifest> manifests = new(StringComparer.OrdinalIgnoreCase);
    private bool stateLoaded;

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
        this.RefreshWatchers();

        var tasks = settingsManager.ConfigurationData.DataSources
            .Where(this.IsSupportedInternalDataSource)
            .Select(this.QueueDataSourceAsync);

        return Task.WhenAll(tasks);
    }

    public Task QueueAllInternalDataSourcesIfAutomaticRefreshAsync()
    {
        if (!settingsManager.ConfigurationData.DataSourceIndexing.AutomaticRefresh)
        {
            this.RefreshWatchers();
            return Task.CompletedTask;
        }

        return this.QueueAllInternalDataSourcesAsync();
    }

    public void RefreshAutomaticWatchers()
    {
        this.RefreshWatchers();
    }

    public async Task QueueDataSourceAsync(IDataSource dataSource)
    {
        if (!this.IsSupportedInternalDataSource(dataSource))
            return;

        logger.LogInformation("Queueing data source '{DataSourceName}' ({DataSourceId}) for background embeddings.", dataSource.Name, dataSource.Id);
        this.RefreshWatchers();
        logger.LogDebug("Adding watcher for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);

        if (!this.statuses.TryGetValue(dataSource.Id, out var currentStatus) || currentStatus.State is not DataSourceEmbeddingState.RUNNING)
            this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.QUEUED, currentStatus?.TotalFiles ?? 0, currentStatus?.IndexedFiles ?? 0, currentStatus?.FailedFiles ?? 0));
        logger.LogDebug("Upserting status for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
        if (this.queuedIds.TryAdd(dataSource.Id, 0))
            await this.queue.Writer.WriteAsync(dataSource.Id);
        logger.LogDebug("Queued data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
    }

    public async Task RemoveDataSourceAsync(IDataSource dataSource)
    {
        if (!this.IsSupportedInternalDataSource(dataSource))
            return;

        this.RemoveWatcher(dataSource.Id);
        this.statuses.TryRemove(dataSource.Id, out _);
        await this.ResetPersistedStateAsync(dataSource.Id, null, CancellationToken.None);
        this.PublishStatusChanged();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.WaitForInitialSettingsAndBootstrapAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var dataSourceId = await this.queue.Reader.ReadAsync(stoppingToken);
            this.queuedIds.TryRemove(dataSourceId, out _);

            var dataSource = settingsManager.ConfigurationData.DataSources
                .FirstOrDefault(source => source.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase));

            if (dataSource is null || !this.IsSupportedInternalDataSource(dataSource))
                continue;

            try
            {
                await this.ProcessDataSourceAsync(dataSource, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Background embedding failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
                this.UpsertStatus(this.GetFallbackStatus(dataSource, exception.Message));
            }
        }
    }

    public override void Dispose()
    {
        this.DisposeWatchers();
        this.stateLock.Dispose();
        base.Dispose();
    }

    private async Task ProcessDataSourceAsync(IDataSource dataSource, CancellationToken token)
    {
        await this.EnsureStateLoadedAsync(token);
        logger.LogInformation("Starting background embeddings for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);

        var vectorStore = await databaseClientProvider.GetVectorStoreAsync(token);

        if (!vectorStore.IsAvailable)
        {
            logger.LogWarning(
                "Skipping background embeddings for data source '{DataSourceName}' ({DataSourceId}) because the database client '{DatabaseName}' is unavailable.",
                dataSource.Name,
                dataSource.Id,
                vectorStore.Name);
            this.UpsertStatus(this.GetFallbackStatus(dataSource, "The vector database is not available."));
            return;
        }

        if (!this.TryResolveEmbeddingProvider(dataSource, out var embeddingProvider))
        {
            this.UpsertStatus(this.GetFallbackStatus(dataSource, "The selected embedding provider is not available."));
            return;
        }

        logger.LogInformation(
            "Using embedding provider '{EmbeddingProviderId}' with model '{EmbeddingModelId}' for data source '{DataSourceName}' ({DataSourceId}).",
            embeddingProvider.Id,
            embeddingProvider.Model.Id,
            dataSource.Name,
            dataSource.Id);

        var collectionName = this.GetCollectionName(dataSource.Id);
        var manifest = await this.EnsureCompatibleManifestAsync(dataSource, embeddingProvider, collectionName, vectorStore, token);
        var inputFiles = this.GetInputFiles(dataSource);
        var indexedFiles = inputFiles.Files;
        var totalFiles = indexedFiles.Count + inputFiles.FailedFiles;

        logger.LogInformation(
            "Prepared data source '{DataSourceName}' ({DataSourceId}) for embedding. AccessibleFiles={AccessibleFiles}, FailedFiles={FailedFiles}, Collection='{CollectionName}'.",
            dataSource.Name,
            dataSource.Id,
            indexedFiles.Count,
            inputFiles.FailedFiles,
            collectionName);

        await this.RemoveMissingFileEmbeddingsAsync(vectorStore, dataSource, collectionName, manifest, indexedFiles, token);
        await this.SaveStateAsync(token);

        this.UpsertStatus(this.CreateStatus(
            dataSource,
            DataSourceEmbeddingState.RUNNING,
            totalFiles,
            0,
            inputFiles.FailedFiles,
            lastError: inputFiles.LastError));

        var provider = embeddingProvider.CreateProvider();
        var skippedFiles = 0;
        var completedFiles = 0;
        var failedFiles = inputFiles.FailedFiles;
        var lastError = inputFiles.LastError;

        foreach (var file in indexedFiles)
        {
            token.ThrowIfCancellationRequested();

            var fingerprint = this.BuildFingerprint(file);
            if (manifest.Files.TryGetValue(file.FullName, out var existingRecord) &&
                string.Equals(existingRecord.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                logger.LogDebug(
                    "Skipping unchanged file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}).",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id);
                skippedFiles++;
                this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, lastError: lastError));
                continue;
            }

            this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, file.Name, lastError));

            try
            {
                logger.LogInformation(
                    "Embedding file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}). Progress={CompletedFiles}/{TotalFiles}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    skippedFiles + completedFiles + 1,
                    totalFiles);
                var startedAtUtc = DateTime.UtcNow;
                var chunkCount = await this.IndexOneFileAsync(vectorStore, dataSource, file, fingerprint, embeddingProvider, provider, manifest, token);
                manifest.Files[file.FullName] = new EmbeddedFileRecord(
                    fingerprint,
                    file.Length,
                    file.LastWriteTimeUtc,
                    DateTime.UtcNow,
                    chunkCount);
                await this.SaveStateAsync(token);
                completedFiles++;
                logger.LogInformation(
                    "Embedded file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) successfully. Chunks={ChunkCount}, DurationMs={DurationMs}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    chunkCount,
                    (DateTime.UtcNow - startedAtUtc).TotalMilliseconds);
            }
            catch (Exception exception)
            {
                failedFiles++;
                lastError = exception.Message;
                manifest.Files.Remove(file.FullName);
                await this.DeleteFilePointsAsync(vectorStore, collectionName, file.FullName, token);
                await this.SaveStateAsync(token);

                logger.LogWarning(exception, "Failed to embed file '{FilePath}' for data source '{DataSourceName}'.", file.FullName, dataSource.Name);
                this.UpsertStatus(this.CreateStatus(dataSource, DataSourceEmbeddingState.RUNNING, totalFiles, skippedFiles + completedFiles, failedFiles, file.Name, exception.Message));
            }
        }

        this.UpsertStatus(this.CreateCompletedStatus(dataSource, totalFiles, skippedFiles + completedFiles, failedFiles, lastError));
        logger.LogInformation(
            "Finished background embeddings for data source '{DataSourceName}' ({DataSourceId}). Indexed={IndexedFiles}, Failed={FailedFiles}, Total={TotalFiles}.",
            dataSource.Name,
            dataSource.Id,
            skippedFiles + completedFiles,
            failedFiles,
            totalFiles);
    }

    private async Task<int> IndexOneFileAsync(
        IVectorStoreClient vectorStore,
        IDataSource dataSource,
        FileInfo file,
        string fingerprint,
        EmbeddingProvider embeddingProvider,
        IProvider provider,
        DataSourceEmbeddingManifest manifest,
        CancellationToken token)
    {
        var collectionName = this.GetCollectionName(dataSource.Id);
        logger.LogDebug(
            "Resetting stored embeddings for file '{FilePath}' in collection '{CollectionName}' before re-indexing.",
            file.FullName,
            collectionName);
        await this.DeleteFilePointsAsync(vectorStore, collectionName, file.FullName, token);

        var batch = new List<(string Text, int ChunkIndex)>(EMBEDDING_BATCH_SIZE);
        var totalChunkCount = 0;

        await foreach (var chunk in this.StreamEmbeddingChunksAsync(file.FullName, token))
        {
            batch.Add((chunk, totalChunkCount));
            totalChunkCount++;

            if (batch.Count >= EMBEDDING_BATCH_SIZE)
                await this.FlushBatchAsync(vectorStore, dataSource, file, fingerprint, embeddingProvider, provider, manifest, collectionName, batch, token);
        }

        if (batch.Count > 0)
            await this.FlushBatchAsync(vectorStore, dataSource, file, fingerprint, embeddingProvider, provider, manifest, collectionName, batch, token);

        if (totalChunkCount == 0)
            throw new InvalidOperationException($"The file '{file.Name}' did not yield any text chunks.");

        logger.LogDebug(
            "Generated {ChunkCount} chunks for file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
            totalChunkCount,
            file.FullName,
            dataSource.Name,
            dataSource.Id);

        return totalChunkCount;
    }

    private async Task FlushBatchAsync(
        IVectorStoreClient vectorStore,
        IDataSource dataSource,
        FileInfo file,
        string fingerprint,
        EmbeddingProvider embeddingProvider,
        IProvider provider,
        DataSourceEmbeddingManifest manifest,
        string collectionName,
        List<(string Text, int ChunkIndex)> batch,
        CancellationToken token)
    {
        logger.LogDebug(
            "Requesting embeddings for batch of {ChunkCount} chunks from file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
            batch.Count,
            file.FullName,
            dataSource.Name,
            dataSource.Id);

        var texts = batch.Select(item => item.Text).ToList();
        var vectors = await provider.EmbedTextAsync(embeddingProvider.Model, settingsManager, token, texts);
        if (vectors.Count != batch.Count)
            throw new InvalidOperationException($"The embedding provider returned {vectors.Count} vectors for {batch.Count} text chunks.");

        var vectorSize = vectors.FirstOrDefault()?.Count ?? 0;
        if (vectorSize <= 0)
            throw new InvalidOperationException("The embedding provider returned an empty vector.");

        if (manifest.VectorSize > 0 && manifest.VectorSize != vectorSize)
            throw new InvalidOperationException($"The embedding vector size changed from {manifest.VectorSize} to {vectorSize}. Please re-save the data source to trigger a clean re-index.");

        if (manifest.VectorSize == 0)
        {
            manifest.VectorSize = vectorSize;
            await this.EnsureCollectionExistsAsync(vectorStore, collectionName, vectorSize, token);
            await this.SaveStateAsync(token);
            logger.LogInformation(
                "Created embedding collection '{CollectionName}' with vector size {VectorSize} for data source '{DataSourceName}' ({DataSourceId}).",
                collectionName,
                vectorSize,
                dataSource.Name,
                dataSource.Id);
        }

        await this.UpsertPointsAsync(
            vectorStore,
            collectionName,
            dataSource,
            file,
            fingerprint,
            batch,
            vectors,
            this.TryGetRelativePath(dataSource, file),
            token);

        logger.LogDebug(
            "Stored {ChunkCount} embedded chunks for file '{FilePath}' in collection '{CollectionName}'.",
            batch.Count,
            file.FullName,
            collectionName);

        batch.Clear();
    }

    private async Task EnsureCollectionExistsAsync(IVectorStoreClient vectorStore, string collectionName, int vectorSize, CancellationToken token)
    {
        await vectorStore.EnsureVectorStoreExists(collectionName, vectorSize, token);
    }

    private async Task UpsertPointsAsync(
        IVectorStoreClient vectorStore,
        string collectionName,
        IDataSource dataSource,
        FileInfo file,
        string fingerprint,
        IReadOnlyList<(string Text, int ChunkIndex)> batch,
        IReadOnlyList<IReadOnlyList<float>> vectors,
        string relativePath,
        CancellationToken token)
    {
        var embeddedAtUtc = DateTime.UtcNow;
        var points = batch.Select((item, index) => new VectorStoragePoint(
            this.CreatePointId(dataSource.Id, fingerprint, item.ChunkIndex),
            vectors[index],
            dataSource.Id,
            dataSource.Name,
            dataSource.Type.ToString(),
            file.FullName,
            file.Name,
            relativePath,
            item.ChunkIndex,
            item.Text,
            fingerprint,
            file.LastWriteTimeUtc,
            embeddedAtUtc)).ToList();

        await vectorStore.InsertEmbedding(collectionName, points, token);
    }

    private async Task DeleteFilePointsAsync(IVectorStoreClient vectorStore, string collectionName, string filePath, CancellationToken token)
    {
        await vectorStore.DeleteEmbeddingByFile(collectionName, filePath, token);
    }

    private async Task DeleteCollectionAsync(string collectionName, IVectorStoreClient? vectorStore, CancellationToken token)
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

        logger.LogInformation("Embedding background service is ready. Checking whether automatic data source refresh is enabled.");
        await this.QueueAllInternalDataSourcesIfAutomaticRefreshAsync();
    }

    private bool IsSupportedInternalDataSource(IDataSource dataSource)
    {
        return dataSource is DataSourceLocalDirectory or DataSourceLocalFile;
    }

    private bool TryResolveEmbeddingProvider(IDataSource dataSource, [NotNullWhen(true)] out EmbeddingProvider? embeddingProvider)
    {
        embeddingProvider = settingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(provider =>
            dataSource is IInternalDataSource internalDataSource &&
            provider.Id.Equals(internalDataSource.EmbeddingId, StringComparison.OrdinalIgnoreCase));

        return embeddingProvider != default && embeddingProvider.UsedLLMProvider is not LLMProviders.NONE;
    }

    private async Task<DataSourceEmbeddingManifest> EnsureCompatibleManifestAsync(IDataSource dataSource, EmbeddingProvider embeddingProvider, string collectionName, IVectorStoreClient vectorStore, CancellationToken token)
    {
        var embeddingSignature = this.BuildEmbeddingSignature(embeddingProvider);
        var manifest = await this.GetManifestAsync(dataSource.Id, token);

        if (!string.Equals(manifest.EmbeddingSignature, embeddingSignature, StringComparison.Ordinal))
        {
            logger.LogInformation(
                "Embedding configuration changed for data source '{DataSourceName}' ({DataSourceId}). Resetting persisted state and collection '{CollectionName}'.",
                dataSource.Name,
                dataSource.Id,
                collectionName);
            await this.ResetPersistedStateAsync(dataSource.Id, vectorStore, token);
            manifest = await this.GetManifestAsync(dataSource.Id, token);
        }

        if (!string.Equals(manifest.EmbeddingProviderId, embeddingProvider.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(manifest.EmbeddingSignature, embeddingSignature, StringComparison.Ordinal))
        {
            manifest.EmbeddingProviderId = embeddingProvider.Id;
            manifest.EmbeddingSignature = embeddingSignature;
            await this.SaveStateAsync(token);
        }

        return manifest;
    }

    private async Task RemoveMissingFileEmbeddingsAsync(
        IVectorStoreClient vectorStore,
        IDataSource dataSource,
        string collectionName,
        DataSourceEmbeddingManifest manifest,
        IReadOnlyCollection<FileInfo> indexedFiles,
        CancellationToken token)
    {
        var existingPaths = indexedFiles
            .Select(file => file.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var removedFilePath in manifest.Files.Keys.Except(existingPaths, StringComparer.OrdinalIgnoreCase).ToList())
        {
            await this.DeleteFilePointsAsync(vectorStore, collectionName, removedFilePath, token);
            manifest.Files.Remove(removedFilePath);
            logger.LogInformation(
                "Removed stale embeddings for deleted file '{FilePath}' from data source '{DataSourceName}' ({DataSourceId}).",
                removedFilePath,
                dataSource.Name,
                dataSource.Id);
        }
    }

    private DataSourceEmbeddingStatus CreateStatus(
        IDataSource dataSource,
        DataSourceEmbeddingState state,
        int totalFiles,
        int indexedFiles,
        int failedFiles,
        string currentFile = "",
        string lastError = "")
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
            lastError);
    }

    private DataSourceEmbeddingStatus CreateCompletedStatus(IDataSource dataSource, int totalFiles, int indexedFiles, int failedFiles, string lastError)
    {
        return this.CreateStatus(
            dataSource,
            failedFiles > 0 ? DataSourceEmbeddingState.FAILED : DataSourceEmbeddingState.COMPLETED,
            totalFiles,
            indexedFiles,
            failedFiles,
            lastError: failedFiles > 0
                ? string.IsNullOrWhiteSpace(lastError)
                    ? "Some files could not be embedded. See the logs for details."
                    : lastError
                : string.Empty);
    }

    private DataSourceEmbeddingStatus GetFallbackStatus(IDataSource dataSource, string errorMessage)
    {
        return this.CreateStatus(dataSource, DataSourceEmbeddingState.FAILED, 0, 0, 1, lastError: errorMessage);
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
