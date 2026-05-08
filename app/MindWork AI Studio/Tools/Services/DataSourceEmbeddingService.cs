using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed class DataSourceEmbeddingService : BackgroundService
{
    private const string STATE_FILENAME = "rag-embedding-state.json";
    private const int MAX_CHUNK_LENGTH = 3_200;
    private const int MIN_CHUNK_LENGTH = 800;
    private const int CHUNK_OVERLAP_LENGTH = 320;
    private const int EMBEDDING_BATCH_SIZE = 16;

    private readonly SettingsManager settingsManager;
    private readonly RustService rustService;
    private readonly EmbeddingStore embeddingStore;
    private readonly ILogger<DataSourceEmbeddingService> logger;
    private readonly Channel<string> queue = Channel.CreateUnbounded<string>();
    private readonly ConcurrentDictionary<string, byte> queuedIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DataSourceEmbeddingStatus> statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FileSystemWatcher> watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim stateLock = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
    };

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceEmbeddingService).Namespace, nameof(DataSourceEmbeddingService));

    private Dictionary<string, DataSourceEmbeddingManifest> manifests = new(StringComparer.OrdinalIgnoreCase);
    private bool stateLoaded;

    public DataSourceEmbeddingService(SettingsManager settingsManager, RustService rustService, EmbeddingStore embeddingStore, ILogger<DataSourceEmbeddingService> logger)
    {
        this.settingsManager = settingsManager;
        this.rustService = rustService;
        this.embeddingStore = embeddingStore;
        this.logger = logger;
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
        var orderedStatuses = this.statuses.Values
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.DataSourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var activeStatus = orderedStatuses
            .FirstOrDefault(status => status.State is DataSourceEmbeddingState.QUEUED or DataSourceEmbeddingState.RUNNING);

        if (activeStatus is not null)
        {
            var total = Math.Max(activeStatus.TotalFiles, 1);
            return new(
                true,
                false,
                activeStatus.State,
                activeStatus.IndexedFiles,
                total,
                activeStatus.FailedFiles,
                $"{TB("Embedding")} {activeStatus.IndexedFiles}/{total}");
        }

        var failedStatus = orderedStatuses
            .FirstOrDefault(status => status.State is DataSourceEmbeddingState.FAILED || status.FailedFiles > 0);

        if (failedStatus is not null)
            return new(true, true, DataSourceEmbeddingState.FAILED, failedStatus.IndexedFiles, failedStatus.TotalFiles, failedStatus.FailedFiles, TB("Embeddings"));

        return new(false, true, DataSourceEmbeddingState.COMPLETED, 0, 0, 0, TB("Embeddings"));
    }

    public Task QueueAllInternalDataSourcesAsync()
    {
        this.RefreshWatchers();

        var tasks = this.settingsManager.ConfigurationData.DataSources
            .Where(this.IsSupportedInternalDataSource)
            .Select(this.QueueDataSourceAsync);

        return Task.WhenAll(tasks);
    }

    public async Task QueueDataSourceAsync(IDataSource dataSource)
    {
        if (!this.IsSupportedInternalDataSource(dataSource))
            return;

        this.logger.LogInformation("Queueing data source '{DataSourceName}' ({DataSourceId}) for background embeddings.", dataSource.Name, dataSource.Id);
        this.RefreshWatchers();

        if (!this.statuses.TryGetValue(dataSource.Id, out var currentStatus) || currentStatus.State is not DataSourceEmbeddingState.RUNNING)
        {
            this.UpsertStatus(new DataSourceEmbeddingStatus(
                dataSource.Id,
                dataSource.Name,
                dataSource.Type,
                DataSourceEmbeddingState.QUEUED,
                currentStatus?.TotalFiles ?? 0,
                currentStatus?.IndexedFiles ?? 0,
                currentStatus?.FailedFiles ?? 0,
                string.Empty,
                string.Empty));
        }

        if (this.queuedIds.TryAdd(dataSource.Id, 0))
            await this.queue.Writer.WriteAsync(dataSource.Id);
    }

    public async Task RemoveDataSourceAsync(IDataSource dataSource)
    {
        if (!this.IsSupportedInternalDataSource(dataSource))
            return;

        this.RemoveWatcher(dataSource.Id);
        this.statuses.TryRemove(dataSource.Id, out _);
        await this.ResetPersistedStateAsync(dataSource.Id);
        this.PublishStatusChanged();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.WaitForInitialSettingsAndBootstrapAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var dataSourceId = await this.queue.Reader.ReadAsync(stoppingToken);
            this.queuedIds.TryRemove(dataSourceId, out _);

            var dataSource = this.settingsManager.ConfigurationData.DataSources
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
                this.logger.LogError(exception, "Background embedding failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
                this.UpsertStatus(this.GetFallbackStatus(dataSource, exception.Message));
            }
        }
    }

    public override void Dispose()
    {
        foreach (var watcher in this.watchers.Values)
            watcher.Dispose();

        this.watchers.Clear();
        this.stateLock.Dispose();
        base.Dispose();
    }

    private async Task ProcessDataSourceAsync(IDataSource dataSource, CancellationToken token)
    {
        await this.EnsureStateLoadedAsync(token);
        this.logger.LogInformation("Starting background embeddings for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);

        if (!this.embeddingStore.IsAvailable)
        {
            this.logger.LogWarning(
                "Skipping background embeddings for data source '{DataSourceName}' ({DataSourceId}) because the database client '{DatabaseName}' is unavailable.",
                dataSource.Name,
                dataSource.Id,
                this.embeddingStore.Name);
            this.UpsertStatus(this.GetFallbackStatus(dataSource, "The vector database is not available."));
            return;
        }

        var embeddingProvider = this.settingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(provider =>
            dataSource is IInternalDataSource internalDataSource &&
            provider.Id.Equals(internalDataSource.EmbeddingId, StringComparison.OrdinalIgnoreCase));

        if (embeddingProvider == default || embeddingProvider.UsedLLMProvider is LLMProviders.NONE)
        {
            this.UpsertStatus(this.GetFallbackStatus(dataSource, "The selected embedding provider is not available."));
            return;
        }

        this.logger.LogInformation(
            "Using embedding provider '{EmbeddingProviderId}' with model '{EmbeddingModelId}' for data source '{DataSourceName}' ({DataSourceId}).",
            embeddingProvider.Id,
            embeddingProvider.Model.Id,
            dataSource.Name,
            dataSource.Id);

        var embeddingSignature = this.BuildEmbeddingSignature(embeddingProvider);
        var manifest = await this.GetManifestAsync(dataSource.Id, token);
        // A provider/model change invalidates the existing collection because the vectors are no longer comparable.
        if (!string.Equals(manifest.EmbeddingSignature, embeddingSignature, StringComparison.Ordinal))
        {
            this.logger.LogInformation(
                "Embedding configuration changed for data source '{DataSourceName}' ({DataSourceId}). Resetting persisted state and collection '{CollectionName}'.",
                dataSource.Name,
                dataSource.Id,
                this.GetCollectionName(dataSource.Id));
            await this.ResetPersistedStateAsync(dataSource.Id);
            manifest = await this.GetManifestAsync(dataSource.Id, token);
            manifest.EmbeddingProviderId = embeddingProvider.Id;
            manifest.EmbeddingSignature = embeddingSignature;
            await this.SaveStateAsync(token);
        }

        var inputFiles = this.GetInputFiles(dataSource);
        var indexedFiles = inputFiles.Files;
        var totalFiles = indexedFiles.Count + inputFiles.FailedFiles;
        var existingPaths = indexedFiles
            .Select(file => file.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        this.logger.LogInformation(
            "Prepared data source '{DataSourceName}' ({DataSourceId}) for embedding. AccessibleFiles={AccessibleFiles}, FailedFiles={FailedFiles}, Collection='{CollectionName}'.",
            dataSource.Name,
            dataSource.Id,
            indexedFiles.Count,
            inputFiles.FailedFiles,
            this.GetCollectionName(dataSource.Id));

        foreach (var removedFilePath in manifest.Files.Keys.Except(existingPaths, StringComparer.OrdinalIgnoreCase).ToList())
        {
            await this.DeleteFilePointsAsync(this.GetCollectionName(dataSource.Id), removedFilePath, token);
            manifest.Files.Remove(removedFilePath);
            this.logger.LogInformation(
                "Removed stale embeddings for deleted file '{FilePath}' from data source '{DataSourceName}' ({DataSourceId}).",
                removedFilePath,
                dataSource.Name,
                dataSource.Id);
        }

        await this.SaveStateAsync(token);

        // Keep the UI status in sync with the long-running file loop below.
        this.UpsertStatus(new DataSourceEmbeddingStatus(
            dataSource.Id,
            dataSource.Name,
            dataSource.Type,
            DataSourceEmbeddingState.RUNNING,
            totalFiles,
            0,
            inputFiles.FailedFiles,
            string.Empty,
            inputFiles.LastError));

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
                this.logger.LogDebug(
                    "Skipping unchanged file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}).",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id);
                skippedFiles++;
                this.UpsertStatus(new DataSourceEmbeddingStatus(
                    dataSource.Id,
                    dataSource.Name,
                    dataSource.Type,
                    DataSourceEmbeddingState.RUNNING,
                    totalFiles,
                    skippedFiles + completedFiles,
                    failedFiles,
                    string.Empty,
                    lastError));
                continue;
            }

            this.UpsertStatus(new DataSourceEmbeddingStatus(
                dataSource.Id,
                dataSource.Name,
                dataSource.Type,
                DataSourceEmbeddingState.RUNNING,
                totalFiles,
                skippedFiles + completedFiles,
                failedFiles,
                file.Name,
                lastError));

            try
            {
                this.logger.LogInformation(
                    "Embedding file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}). Progress={CompletedFiles}/{TotalFiles}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    skippedFiles + completedFiles + 1,
                    totalFiles);
                var startedAtUtc = DateTime.UtcNow;
                var chunkCount = await this.IndexOneFileAsync(dataSource, file, fingerprint, embeddingProvider, provider, manifest, token);
                manifest.Files[file.FullName] = new EmbeddedFileRecord(
                    fingerprint,
                    file.Length,
                    file.LastWriteTimeUtc,
                    DateTime.UtcNow,
                    chunkCount);
                await this.SaveStateAsync(token);
                completedFiles++;
                this.logger.LogInformation(
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
                await this.DeleteFilePointsAsync(this.GetCollectionName(dataSource.Id), file.FullName, token);
                await this.SaveStateAsync(token);

                this.logger.LogWarning(exception, "Failed to embed file '{FilePath}' for data source '{DataSourceName}'.", file.FullName, dataSource.Name);
                this.UpsertStatus(new DataSourceEmbeddingStatus(
                    dataSource.Id,
                    dataSource.Name,
                    dataSource.Type,
                    DataSourceEmbeddingState.RUNNING,
                    totalFiles,
                    skippedFiles + completedFiles,
                    failedFiles,
                    file.Name,
                    exception.Message));
            }
        }

        this.UpsertStatus(new DataSourceEmbeddingStatus(
            dataSource.Id,
            dataSource.Name,
            dataSource.Type,
            failedFiles > 0 ? DataSourceEmbeddingState.FAILED : DataSourceEmbeddingState.COMPLETED,
            totalFiles,
            skippedFiles + completedFiles,
            failedFiles,
            string.Empty,
            failedFiles > 0
                ? string.IsNullOrWhiteSpace(lastError)
                    ? "Some files could not be embedded. See the logs for details."
                    : lastError
                : string.Empty));
        this.logger.LogInformation(
            "Finished background embeddings for data source '{DataSourceName}' ({DataSourceId}). Indexed={IndexedFiles}, Failed={FailedFiles}, Total={TotalFiles}.",
            dataSource.Name,
            dataSource.Id,
            skippedFiles + completedFiles,
            failedFiles,
            totalFiles);
    }

    private async Task<int> IndexOneFileAsync(
        IDataSource dataSource,
        FileInfo file,
        string fingerprint,
        EmbeddingProvider embeddingProvider,
        IProvider provider,
        DataSourceEmbeddingManifest manifest,
        CancellationToken token)
    {
        var collectionName = this.GetCollectionName(dataSource.Id);
        this.logger.LogDebug(
            "Resetting stored embeddings for file '{FilePath}' in collection '{CollectionName}' before re-indexing.",
            file.FullName,
            collectionName);
        await this.DeleteFilePointsAsync(collectionName, file.FullName, token);

        var batch = new List<(string Text, int ChunkIndex)>(EMBEDDING_BATCH_SIZE);
        var totalChunkCount = 0;

        await foreach (var chunk in this.StreamEmbeddingChunksAsync(file.FullName, token))
        {
            batch.Add((chunk, totalChunkCount));
            totalChunkCount++;

            if (batch.Count >= EMBEDDING_BATCH_SIZE)
                await this.FlushBatchAsync(dataSource, file, fingerprint, embeddingProvider, provider, manifest, collectionName, batch, token);
        }

        if (batch.Count > 0)
            await this.FlushBatchAsync(dataSource, file, fingerprint, embeddingProvider, provider, manifest, collectionName, batch, token);

        if (totalChunkCount == 0)
            throw new InvalidOperationException($"The file '{file.Name}' did not yield any text chunks.");

        this.logger.LogDebug(
            "Generated {ChunkCount} chunks for file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
            totalChunkCount,
            file.FullName,
            dataSource.Name,
            dataSource.Id);

        return totalChunkCount;
    }

    private async Task FlushBatchAsync(
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
        this.logger.LogDebug(
            "Requesting embeddings for batch of {ChunkCount} chunks from file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
            batch.Count,
            file.FullName,
            dataSource.Name,
            dataSource.Id);

        var texts = batch.Select(item => item.Text).ToList();
        var vectors = await provider.EmbedTextAsync(embeddingProvider.Model, this.settingsManager, token, texts);
        if (vectors.Count != batch.Count)
            throw new InvalidOperationException($"The embedding provider returned {vectors.Count} vectors for {batch.Count} text chunks.");

        var vectorSize = vectors.FirstOrDefault()?.Count ?? 0;
        if (vectorSize <= 0)
            throw new InvalidOperationException("The embedding provider returned an empty vector.");

        if (manifest.VectorSize > 0 && manifest.VectorSize != vectorSize)
            throw new InvalidOperationException($"The embedding vector size changed from {manifest.VectorSize} to {vectorSize}. Please re-save the data source to trigger a clean re-index.");

        if (manifest.VectorSize == 0)
        {
            // The first successful batch defines the vector size for the whole collection.
            manifest.VectorSize = vectorSize;
            await this.EnsureCollectionExistsAsync(collectionName, vectorSize, token);
            await this.SaveStateAsync(token);
            this.logger.LogInformation(
                "Created embedding collection '{CollectionName}' with vector size {VectorSize} for data source '{DataSourceName}' ({DataSourceId}).",
                collectionName,
                vectorSize,
                dataSource.Name,
                dataSource.Id);
        }

        await this.UpsertPointsAsync(
            collectionName,
            dataSource,
            file,
            fingerprint,
            batch,
            vectors,
            this.TryGetRelativePath(dataSource, file),
            token);

        this.logger.LogDebug(
            "Stored {ChunkCount} embedded chunks for file '{FilePath}' in collection '{CollectionName}'.",
            batch.Count,
            file.FullName,
            collectionName);

        batch.Clear();
    }

    private async IAsyncEnumerable<string> StreamEmbeddingChunksAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        if (this.IsImageFilePath(filePath))
        {
            yield return this.BuildImageIndexText(filePath);
            yield break;
        }

        var currentChunk = new StringBuilder();

        await foreach (var segment in this.rustService.StreamArbitraryFileData(filePath, token: token))
        {
            var normalized = NormalizeChunkSegment(segment);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (currentChunk.Length > 0 && currentChunk.Length + normalized.Length + Environment.NewLine.Length > MAX_CHUNK_LENGTH)
            {
                if (currentChunk.Length >= MIN_CHUNK_LENGTH)
                {
                    var chunk = currentChunk.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(chunk))
                        yield return chunk;

                    var overlap = chunk.Length > CHUNK_OVERLAP_LENGTH
                        ? chunk[^CHUNK_OVERLAP_LENGTH..]
                        : chunk;

                    currentChunk.Clear();
                    currentChunk.Append(overlap);
                    currentChunk.AppendLine();
                }
                else
                {
                    currentChunk.AppendLine();
                }
            }

            currentChunk.Append(normalized);
            currentChunk.AppendLine();
        }

        var finalChunk = currentChunk.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(finalChunk))
            yield return finalChunk;
    }

    private async Task EnsureCollectionExistsAsync(string collectionName, int vectorSize, CancellationToken token)
    {
        await this.embeddingStore.EnsureEmbeddingStoreExists(collectionName, vectorSize, token);
    }

    private async Task UpsertPointsAsync(
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
        var points = batch.Select((item, index) => new EmbeddingStoragePoint(
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

        await this.embeddingStore.InsertEmbedding(collectionName, points, token);
    }

    private async Task DeleteFilePointsAsync(string collectionName, string filePath, CancellationToken token)
    {
        await this.embeddingStore.DeleteEmbeddingByFile(collectionName, filePath, token);
    }

    private async Task DeleteCollectionAsync(string collectionName)
    {
        await this.embeddingStore.DeleteEmbeddingStore(collectionName, CancellationToken.None);
    }

    private async Task EnsureStateLoadedAsync(CancellationToken token)
    {
        if (this.stateLoaded)
            return;

        await this.stateLock.WaitAsync(token);
        try
        {
            if (this.stateLoaded)
                return;

            var statePath = this.GetStatePath();
            if (!string.IsNullOrWhiteSpace(statePath) && File.Exists(statePath))
            {
                var json = await File.ReadAllTextAsync(statePath, token);
                var persistedState = JsonSerializer.Deserialize<PersistedEmbeddingState>(json, this.jsonOptions);
                this.manifests = persistedState?.DataSources ?? new Dictionary<string, DataSourceEmbeddingManifest>(StringComparer.OrdinalIgnoreCase);
            }

            this.stateLoaded = true;
        }
        finally
        {
            this.stateLock.Release();
        }
    }

    private async Task<DataSourceEmbeddingManifest> GetManifestAsync(string dataSourceId, CancellationToken token)
    {
        await this.EnsureStateLoadedAsync(token);
        if (this.manifests.TryGetValue(dataSourceId, out var manifest))
            return manifest;

        manifest = new DataSourceEmbeddingManifest();
        this.manifests[dataSourceId] = manifest;
        return manifest;
    }

    private async Task SaveStateAsync(CancellationToken token)
    {
        var statePath = this.GetStatePath();
        if (string.IsNullOrWhiteSpace(statePath))
            return;

        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var persistedState = new PersistedEmbeddingState
        {
            DataSources = this.manifests
        };

        var json = JsonSerializer.Serialize(persistedState, this.jsonOptions);
        await File.WriteAllTextAsync(statePath, json, token);
    }

    private async Task ResetPersistedStateAsync(string dataSourceId)
    {
        await this.EnsureStateLoadedAsync(CancellationToken.None);
        this.manifests.Remove(dataSourceId);
        await this.DeleteCollectionAsync(this.GetCollectionName(dataSourceId));
        await this.SaveStateAsync(CancellationToken.None);
        this.logger.LogInformation("Reset persisted embedding state for data source '{DataSourceId}'.", dataSourceId);
    }

    private async Task WaitForInitialSettingsAndBootstrapAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (this.settingsManager.HasCompletedInitialSettingsLoad
                && !string.IsNullOrWhiteSpace(SettingsManager.ConfigDirectory)
                && !string.IsNullOrWhiteSpace(SettingsManager.DataDirectory))
            {
                break;
            }

            await Task.Delay(250, token);
        }

        token.ThrowIfCancellationRequested();

        this.logger.LogInformation("Embedding background service is ready. Queueing all internal data sources.");
        await this.QueueAllInternalDataSourcesAsync();
    }

    private void RefreshWatchers()
    {
        var supportedSources = this.settingsManager.ConfigurationData.DataSources
            .Where(this.IsSupportedInternalDataSource)
            .ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var existingWatcherId in this.watchers.Keys.Except(supportedSources.Keys, StringComparer.OrdinalIgnoreCase).ToList())
            this.RemoveWatcher(existingWatcherId);

        foreach (var dataSource in supportedSources.Values)
            this.EnsureWatcher(dataSource);
    }

    private void EnsureWatcher(IDataSource dataSource)
    {
        if (this.watchers.TryGetValue(dataSource.Id, out var existingWatcher))
        {
            var existingTarget = $"{existingWatcher.Path}|{existingWatcher.Filter}|{existingWatcher.IncludeSubdirectories}";
            var desiredTarget = this.GetWatcherTarget(dataSource);
            if (string.Equals(existingTarget, desiredTarget, StringComparison.OrdinalIgnoreCase))
                return;

            this.RemoveWatcher(dataSource.Id);
        }

        var watcher = this.CreateWatcher(dataSource);
        if (watcher is null)
            return;

        if (!this.watchers.TryAdd(dataSource.Id, watcher))
            watcher.Dispose();
    }

    private FileSystemWatcher? CreateWatcher(IDataSource dataSource)
    {
        FileSystemWatcher? watcher = dataSource switch
        {
            DataSourceLocalDirectory localDirectory when Directory.Exists(localDirectory.Path) => new FileSystemWatcher(localDirectory.Path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            },
            DataSourceLocalFile localFile when File.Exists(localFile.FilePath) && !string.IsNullOrWhiteSpace(Path.GetDirectoryName(localFile.FilePath)) => new FileSystemWatcher(Path.GetDirectoryName(localFile.FilePath)!)
            {
                Filter = Path.GetFileName(localFile.FilePath),
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            },
            _ => null,
        };

        if (watcher is null)
            return null;

        FileSystemEventHandler onChanged = (_, _) => this.OnWatchedDataSourceChanged(dataSource.Id);
        RenamedEventHandler onRenamed = (_, _) => this.OnWatchedDataSourceChanged(dataSource.Id);
        ErrorEventHandler onError = (_, errorArgs) =>
        {
            this.logger.LogWarning(errorArgs.GetException(), "The file watcher for data source '{DataSourceId}' failed. Recreating it.", dataSource.Id);
            this.RemoveWatcher(dataSource.Id);
            this.EnsureWatcher(dataSource);
            this.OnWatchedDataSourceChanged(dataSource.Id);
        };

        watcher.Created += onChanged;
        watcher.Changed += onChanged;
        watcher.Deleted += onChanged;
        watcher.Renamed += onRenamed;
        watcher.Error += onError;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private string GetWatcherTarget(IDataSource dataSource) => dataSource switch
    {
        DataSourceLocalDirectory localDirectory => $"{localDirectory.Path}|*.*|True",
        DataSourceLocalFile localFile => $"{Path.GetDirectoryName(localFile.FilePath)}|{Path.GetFileName(localFile.FilePath)}|False",
        _ => string.Empty
    };

    private void RemoveWatcher(string dataSourceId)
    {
        if (this.watchers.TryRemove(dataSourceId, out var watcher))
            watcher.Dispose();
    }

    private void OnWatchedDataSourceChanged(string dataSourceId)
    {
        this.logger.LogInformation("Detected file system change for data source '{DataSourceId}'. Queueing a fresh embedding run.", dataSourceId);
        _ = Task.Run(async () =>
        {
            try
            {
                var dataSource = this.settingsManager.ConfigurationData.DataSources
                    .FirstOrDefault(source => source.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase));

                if (dataSource is not null)
                    await this.QueueDataSourceAsync(dataSource);
            }
            catch (Exception exception)
            {
                this.logger.LogWarning(exception, "Failed to queue watched data source '{DataSourceId}' after a file system change.", dataSourceId);
            }
        });
    }

    private string GetStatePath()
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.ConfigDirectory))
            return string.Empty;

        return Path.Combine(SettingsManager.ConfigDirectory, STATE_FILENAME);
    }

    private FileEnumerationResult GetInputFiles(IDataSource dataSource)
    {
        var result = new FileEnumerationResult();

        switch (dataSource)
        {
            case DataSourceLocalFile localFile when File.Exists(localFile.FilePath):
                result.Files.Add(new FileInfo(localFile.FilePath));
                return result;

            case DataSourceLocalDirectory localDirectory when Directory.Exists(localDirectory.Path):
                this.EnumerateAccessibleFiles(localDirectory.Path, result);
                return result;
        }

        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                result.FailedFiles = 1;
                result.LastError = $"The selected file '{localFile.FilePath}' does not exist.";
                break;

            case DataSourceLocalDirectory localDirectory:
                result.FailedFiles = 1;
                result.LastError = $"The selected directory '{localDirectory.Path}' does not exist.";
                break;
        }

        return result;
    }

    private void EnumerateAccessibleFiles(string rootPath, FileEnumerationResult result)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count > 0)
        {
            var currentPath = pendingDirectories.Pop();
            IEnumerable<string> subDirectories;
            IEnumerable<string> files;

            try
            {
                subDirectories = Directory.EnumerateDirectories(currentPath);
                files = Directory.EnumerateFiles(currentPath);
            }
            catch (Exception exception)
            {
                this.logger.LogWarning(exception, "Cannot access directory '{DirectoryPath}' while indexing.", currentPath);
                result.FailedFiles++;
                result.LastError = $"The directory '{currentPath}' could not be accessed.";
                continue;
            }

            foreach (var filePath in files)
            {
                FileInfo fileInfo;
                try
                {
                    fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists)
                        continue;
                }
                catch (Exception exception)
                {
                    this.logger.LogWarning(exception, "Cannot inspect file '{FilePath}' while indexing.", filePath);
                    result.FailedFiles++;
                    result.LastError = $"The file '{filePath}' could not be inspected.";
                    continue;
                }

                result.Files.Add(fileInfo);
            }

            foreach (var subDirectory in subDirectories)
                pendingDirectories.Push(subDirectory);
        }
    }

    private string TryGetRelativePath(IDataSource dataSource, FileInfo file) => dataSource switch
    {
        DataSourceLocalDirectory localDirectory => Path.GetRelativePath(localDirectory.Path, file.FullName),
        _ => file.Name
    };

    private static string NormalizeChunkSegment(string input)
    {
        return input
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private bool IsImageFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.IMAGE);
    }

    private string BuildImageIndexText(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath).TrimStart('.');
        var normalizedName = fileNameWithoutExtension
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

        return $$"""
                 Image asset
                 File name: {{fileName}}
                 Type: {{extension}}
                 Search terms: {{normalizedName}}
                 Path: {{filePath}}
                 Note: The current RAG embedding pipeline stores image files by metadata only. Visual content is not OCRed or captioned yet.
                 """;
    }

    private string BuildEmbeddingSignature(EmbeddingProvider embeddingProvider)
    {
        return string.Join('|',
            embeddingProvider.Id,
            embeddingProvider.UsedLLMProvider,
            embeddingProvider.Model.Id,
            embeddingProvider.Host,
            embeddingProvider.Hostname,
            embeddingProvider.TokenizerPath);
    }

    private string BuildFingerprint(FileInfo file)
    {
        var fingerprintSource = $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource));
        return Convert.ToHexString(bytes);
    }

    private string GetCollectionName(string dataSourceId)
    {
        var safeId = dataSourceId
            .ToLowerInvariant()
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return $"rag_{safeId}";
    }

    private string CreatePointId(string dataSourceId, string fingerprint, int chunkIndex)
    {
        var source = $"{dataSourceId}:{fingerprint}:{chunkIndex}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var guidBytes = hash[..16].ToArray();

        // Mark the bytes as an RFC 4122 version 4 UUID so Qdrant accepts the ID format.
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes).ToString();
    }

    private bool IsSupportedInternalDataSource(IDataSource dataSource)
    {
        return dataSource is DataSourceLocalDirectory or DataSourceLocalFile;
    }

    private DataSourceEmbeddingStatus GetFallbackStatus(IDataSource dataSource, string errorMessage)
    {
        return new DataSourceEmbeddingStatus(
            dataSource.Id,
            dataSource.Name,
            dataSource.Type,
            DataSourceEmbeddingState.FAILED,
            0,
            0,
            1,
            string.Empty,
            errorMessage);
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

public sealed record DataSourceEmbeddingOverview(
    bool IsVisible,
    bool ShowIconOnly,
    DataSourceEmbeddingState State,
    int IndexedFiles,
    int TotalFiles,
    int FailedFiles,
    string NavLabel);

public enum DataSourceEmbeddingState
{
    IDLE,
    QUEUED,
    RUNNING,
    COMPLETED,
    FAILED,
}

public sealed record DataSourceEmbeddingStatus(
    string DataSourceId,
    string DataSourceName,
    DataSourceType DataSourceType,
    DataSourceEmbeddingState State,
    int TotalFiles,
    int IndexedFiles,
    int FailedFiles,
    string CurrentFile,
    string LastError)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceEmbeddingService).Namespace, nameof(DataSourceEmbeddingService));

    public int ProgressPercent => this.TotalFiles <= 0 ? 0 : Math.Clamp((int)Math.Round(this.IndexedFiles * 100d / this.TotalFiles), 0, 100);

    public string StateLabel => this.State switch
    {
        DataSourceEmbeddingState.QUEUED => TB("Queued"),
        DataSourceEmbeddingState.RUNNING => TB("Running"),
        DataSourceEmbeddingState.COMPLETED => TB("Completed"),
        DataSourceEmbeddingState.FAILED => TB("Needs attention"),
        _ => TB("Idle")
    };

    public int SortOrder => this.State switch
    {
        DataSourceEmbeddingState.RUNNING => 0,
        DataSourceEmbeddingState.QUEUED => 1,
        DataSourceEmbeddingState.FAILED => 2,
        DataSourceEmbeddingState.COMPLETED => 3,
        _ => 4,
    };
}

public sealed class FileEnumerationResult
{
    public List<FileInfo> Files { get; } = [];

    public int FailedFiles { get; set; }

    public string LastError { get; set; } = string.Empty;
}

public sealed class PersistedEmbeddingState
{
    public Dictionary<string, DataSourceEmbeddingManifest> DataSources { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DataSourceEmbeddingManifest
{
    public string EmbeddingProviderId { get; set; } = string.Empty;

    public string EmbeddingSignature { get; set; } = string.Empty;

    public int VectorSize { get; set; }

    public Dictionary<string, EmbeddedFileRecord> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record EmbeddedFileRecord(
    string Fingerprint,
    long FileSize,
    DateTime LastWriteUtc,
    DateTime EmbeddedAtUtc,
    int ChunkCount);
