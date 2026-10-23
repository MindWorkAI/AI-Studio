using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Security;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One indexing run of a data source: what it works with, and what it does to the stores.
/// </summary>
/// <remarks>
/// Worked out once before the run starts, after the stores, the embedding provider and the stored
/// manifest were all found to be usable. Whatever kind of data source is indexed, these are the same
/// things, and so is cleaning up after a document. Keeping them here lets each kind of data source
/// bring only what is its own: how it finds its documents and how it reads them.
///
/// A document is addressed by its key, which is what its vectors and its row in the index store are
/// filed under. For a file, that is its full path.
/// </remarks>
/// <param name="dataSource">The data source being indexed.</param>
/// <param name="embeddingProvider">The embedding provider the data source points at.</param>
/// <param name="provider">The provider instance which embeds the text.</param>
/// <param name="vectorStore">The vector store, known to be available.</param>
/// <param name="indexStore">The index store, known to be available.</param>
/// <param name="manifest">What the index stores about the data source, made to match the current embedding configuration.</param>
/// <param name="share">How much work the run does at most, as its kind of data source says.</param>
/// <param name="pass">The pass the run is a step of, which knows what failed during the steps before.</param>
/// <param name="promptInjectionScope">The run as one action of the prompt injection filter, which tells the user about everything it removed once the run is over.</param>
/// <param name="settingsManager">The settings, which the provider reads when it embeds.</param>
/// <param name="publishStatus">Hands a new status of the data source to the embedding service.</param>
/// <param name="logger">The logger of the embedding service, so the log reads the same whoever writes it.</param>
internal sealed class IndexedRunContext(IIndexedDataSource dataSource, EmbeddingProvider embeddingProvider, IProvider provider, VectorStoreClient vectorStore, IndexStoreClient indexStore, DataSourceEmbeddingManifest manifest, RunShare share, IndexingPass pass, PromptInjectionReportingScope promptInjectionScope, SettingsManager settingsManager, Action<DataSourceEmbeddingStatus> publishStatus, ILogger logger)
{
    /// <summary>
    /// After how many stored chunks the collection is optimized while a run is still going.
    /// </summary>
    private const int VECTOR_STORE_OPTIMIZATION_CHUNK_THRESHOLD = 100_000;

    private long storedChunksSinceLastOptimization;
    private bool hasPendingChanges;

    private Dictionary<string, string>? fileParentIds;
    private HashSet<string>? reservedFileParentIds;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(IndexedRunContext).Namespace, nameof(IndexedRunContext));

    /// <summary>
    /// One chunk on its way to the stores, with the id it is stored under and the hash of its text.
    /// </summary>
    private sealed record EmbeddingChunkDraft(string ChunkId, string Text, string TextHash, int ChunkIndex, int? PageNumber, int? EndPageNumber);

    public IIndexedDataSource DataSource => dataSource;

    public EmbeddingProvider EmbeddingProvider => embeddingProvider;

    public IProvider Provider => provider;

    public VectorStoreClient VectorStore => vectorStore;

    public IndexStoreClient IndexStore => indexStore;

    public DataSourceEmbeddingManifest Manifest => manifest;

    public string CollectionName { get; } = DataSourceEmbeddingNames.GetCollectionName(dataSource.Id);

    public RunShare Share => share;

    public IndexingPass Pass => pass;

    /// <summary>
    /// The id a file is stored under: the one it already has, or a new one.
    /// </summary>
    /// <remarks>
    /// A relocated file keeps the id it got when it was indexed first, which came from its path back
    /// then. A new file may now appear at that very path, so the ids already stored are reserved, and
    /// a new file whose derived id is taken gets a random one instead. Its chunk ids include that id,
    /// cf. IndexedDocumentIds.CreateChunkIdSource.
    /// </remarks>
    /// <param name="documentKey">The key of the file, which is its absolute path.</param>
    /// <returns>The parent id of the file.</returns>
    public string GetFileParentId(string documentKey)
    {
        if (this.fileParentIds is null)
        {
            this.fileParentIds = manifest.Files
                .Where(file => file.Value.ParentFileId is not null)
                .ToDictionary(file => file.Key, file => file.Value.ParentFileId!, DataSourceEmbeddingManifest.PATH_COMPARER);
            this.reservedFileParentIds = this.fileParentIds.Values.ToHashSet(StringComparer.Ordinal);
        }

        if (this.fileParentIds.TryGetValue(documentKey, out var storedId))
            return storedId;

        var parentId = IndexedDocumentIds.CreateParentId(dataSource.Id, documentKey);
        while (!this.reservedFileParentIds!.Add(parentId))
            parentId = Guid.NewGuid().ToString();

        this.fileParentIds[documentKey] = parentId;
        return parentId;
    }

    /// <summary>
    /// The run as one action of the prompt injection filter.
    /// </summary>
    /// <remarks>
    /// Everything read for the run carries it down to the filter, so a run over a few thousand
    /// documents which removes something in forty of them tells the user once, when it is over,
    /// rather than forty times. Every kind of data source gets this from here, so none can forget it.
    /// </remarks>
    public PromptInjectionReportingScope PromptInjectionScope => promptInjectionScope;

    /// <summary>
    /// When the run first asked whether it did its share of the work, or null while it never did.
    /// </summary>
    /// <remarks>
    /// The clock of the share starts then, cf. DocumentRunProgress.HasDoneItsShare. The embedding
    /// service reads it once the run is over, to notice a kind of data source which never asks.
    /// </remarks>
    public DateTimeOffset? ShareStartedUtc { get; private set; }

    /// <summary>
    /// Starts the clock of the share of the work, unless it runs already.
    /// </summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>When the clock started.</returns>
    public DateTimeOffset StartShareClock(DateTimeOffset nowUtc) => this.ShareStartedUtc ??= nowUtc;

    /// <summary>
    /// Tells the user interface about a new status of the data source.
    /// </summary>
    /// <param name="status">The status.</param>
    public void PublishStatus(DataSourceEmbeddingStatus status) => publishStatus(status);

    /// <summary>
    /// Embeds one document and stores it, in place of whatever was stored for it before.
    /// </summary>
    /// <remarks>
    /// Which chunks the document has stored is read first. Then the old index row goes, and the row
    /// is written anew with a chunk count of zero, so the chunks have something to point at while
    /// they arrive batch by batch. Each chunk whose text the document had before takes over the
    /// stored vector instead of going to the embedding provider again, so the old vectors stay until
    /// all new chunks are stored, and only then go. The new chunks belong to the next generation and
    /// have ids of their own, so none of them overwrites an old one. The final row, with the real chunk count, is
    /// written by whoever decides that the document was indexed: only the kind of data source knows
    /// whether it changed in the meantime.
    /// </remarks>
    /// <param name="document">The document to index.</param>
    /// <param name="reportBlockProgress">Told about every chunk, with its number and its page.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The number of chunks stored for the document.</returns>
    public async Task<int> IndexDocumentAsync(EmbeddingDocument document, Action<int, int?> reportBlockProgress, CancellationToken token)
    {
        var reusableVectors = await ReusableVectors.ReadAsync(indexStore, dataSource.Id, document.Key, token);

        logger.LogDebug(
            "Resetting the stored chunks of file '{FilePath}' in collection '{CollectionName}' before re-indexing. Its vectors stay until the new ones are stored.",
            document.Key,
            this.CollectionName);
        await indexStore.DeleteFileAsync(dataSource.Id, document.Key, token);
        await indexStore.UpsertFileAsync(dataSource.Id, document.State, token);

        var embeddingBatchSize = Math.Max(1, embeddingProvider.EffectiveEmbeddingBatchSize);
        var batch = new List<EmbeddingChunkDraft>(embeddingBatchSize);
        var totalChunkCount = 0;
        var reusedChunkCount = 0;
        var storedChunkIds = new List<string>();
        var chunkIdSource = IndexedDocumentIds.CreateChunkIdSource(dataSource.Id, document.Key, document.State.ParentFileId, document.State.Fingerprint, reusableVectors.NextGeneration);

        await foreach (var chunk in document.StreamChunks(token))
        {
            var chunkId = IndexedDocumentIds.CreateChunkId(dataSource.Id, chunkIdSource, totalChunkCount);
            batch.Add(new(chunkId, chunk.Text, ChunkTextHash.Of(chunk.Text), totalChunkCount, chunk.PageNumber, chunk.EndPageNumber));
            storedChunkIds.Add(chunkId);
            totalChunkCount++;
            reportBlockProgress(totalChunkCount, chunk.PageNumber);

            if (batch.Count >= embeddingBatchSize)
                reusedChunkCount += await this.FlushBatchAsync(document, batch, reusableVectors, token);
        }

        if (batch.Count > 0)
            reusedChunkCount += await this.FlushBatchAsync(document, batch, reusableVectors, token);

        //
        // The extraction itself did not report a failure, but nothing usable came out of it. For
        // the index this is the same case as a scanned page without a text layer, which is why it
        // carries a code of its own instead of an unclassified exception:
        //
        if (totalChunkCount == 0)
            throw new FileExtractionException(FileExtractionErrorCode.NO_CONTENT, string.Format(TB("No text could be read from '{0}'."), document.Key));

        //
        // Only now the old vectors of the document go: every point it has, apart from those just
        // written. That includes a point an interrupted run left behind for the document.
        //
        await this.DeleteDocumentPointsAsync(document.Key, storedChunkIds, token);

        logger.LogDebug(
            "Generated {ChunkCount} chunks for file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
            totalChunkCount,
            document.Key,
            dataSource.Name,
            dataSource.Id);

        if (reusedChunkCount > 0)
            logger.LogInformation(
                "Reused the stored vectors of {ReusedChunkCount} of {ChunkCount} chunks for file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}). Only the other chunks went to the embedding provider.",
                reusedChunkCount,
                totalChunkCount,
                document.Key,
                dataSource.Name,
                dataSource.Id);

        return totalChunkCount;
    }

    /// <summary>
    /// Removes the vectors of one document from the collection.
    /// </summary>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="token">The cancellation token.</param>
    public Task DeleteDocumentPointsAsync(string documentKey, CancellationToken token) => this.DeleteDocumentPointsAsync(documentKey, [], token);

    /// <summary>
    /// Removes the vectors of one document from the collection, apart from the given ones.
    /// </summary>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="keepPointIds">The points of the document which stay.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task DeleteDocumentPointsAsync(string documentKey, IReadOnlyCollection<string> keepPointIds, CancellationToken token)
    {
        // A relocated document keeps its original parent id. Delete by that identity as well as
        // by path, including when a previous relocation stopped between its two database writes.
        var parentId = manifest.Files.GetValueOrDefault(documentKey)?.ParentFileId ?? string.Empty;
        await vectorStore.DeleteEmbeddingByFile(this.CollectionName, documentKey, parentId, keepPointIds, token);
        this.hasPendingChanges = true;
    }

    /// <summary>
    /// Removes whatever a failed attempt left behind of one document.
    /// </summary>
    /// <remarks>
    /// Never throws for a store which refuses: the document already failed, and that failure is the
    /// one the user has to hear about.
    /// </remarks>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task CleanupFailedDocumentAsync(string documentKey, CancellationToken token)
    {
        try
        {
            await this.DeleteDocumentPointsAsync(documentKey, token);
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
                documentKey,
                dataSource.Name,
                dataSource.Id);
        }

        try
        {
            await indexStore.DeleteFileAsync(dataSource.Id, documentKey, token);
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
                documentKey,
                dataSource.Name,
                dataSource.Id);
        }
    }

    /// <summary>
    /// Drops the mark which keeps a document out of the index, in the store as well as in the manifest.
    /// </summary>
    /// <remarks>
    /// Called whenever a document was read, and whenever it failed for a reason outside of itself. The
    /// state heals on its own that way: a document which becomes readable, or a drive which comes
    /// back, leaves nothing behind.
    /// </remarks>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task ForgetPermanentFailureAsync(string documentKey, CancellationToken token)
    {
        if (!manifest.PermanentFailures.Remove(documentKey))
            return;

        await indexStore.DeletePermanentFailureAsync(dataSource.Id, documentKey, token);
        logger.LogDebug(
            "Removed the permanent indexing failure of file '{FilePath}' from data source '{DataSourceName}' ({DataSourceId}).",
            documentKey,
            dataSource.Name,
            dataSource.Id);
    }

    /// <summary>
    /// Counts chunks which reached the collection, and optimizes it once enough of them came together.
    /// </summary>
    /// <param name="chunkCount">How many chunks were just stored.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task RecordStoredChunksAsync(int chunkCount, CancellationToken token)
    {
        if (chunkCount > 0)
        {
            this.hasPendingChanges = true;
            this.storedChunksSinceLastOptimization += chunkCount;
        }

        if (this.storedChunksSinceLastOptimization >= VECTOR_STORE_OPTIMIZATION_CHUNK_THRESHOLD)
            await this.OptimizeCollectionIfNeededAsync("stored chunk threshold reached", token);
    }

    /// <summary>
    /// Optimizes the collection when anything in it changed since the last time.
    /// </summary>
    /// <param name="reason">Why it is asked for now, for the log.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task OptimizeCollectionIfNeededAsync(string reason, CancellationToken token)
    {
        if (!this.hasPendingChanges)
            return;

        logger.LogInformation(
            "Optimizing embedding collection '{CollectionName}' for data source '{DataSourceName}' ({DataSourceId}). Reason='{Reason}', StoredChunksSinceLastOptimization={StoredChunksSinceLastOptimization}, ChunkThreshold={ChunkThreshold}.",
            this.CollectionName,
            dataSource.Name,
            dataSource.Id,
            reason,
            this.storedChunksSinceLastOptimization,
            VECTOR_STORE_OPTIMIZATION_CHUNK_THRESHOLD);

        await vectorStore.OptimizeVectorStore(this.CollectionName, token);
        this.storedChunksSinceLastOptimization = 0;
        this.hasPendingChanges = false;
    }

    /// <summary>
    /// Stores one batch of chunks, each with its vector.
    /// </summary>
    /// <param name="document">The document the chunks belong to.</param>
    /// <param name="batch">The chunks, emptied once they are stored.</param>
    /// <param name="reusableVectors">The chunks the document had stored before.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>How many of the chunks kept a stored vector instead of being embedded.</returns>
    private async Task<int> FlushBatchAsync(EmbeddingDocument document, List<EmbeddingChunkDraft> batch, ReusableVectors reusableVectors, CancellationToken token)
    {
        //
        // A chunk whose text the document had stored before takes that vector over. Only the other
        // chunks go to the embedding provider:
        //
        var storedVectors = await reusableVectors.TakeAsync(vectorStore, this.CollectionName, manifest.VectorSize, batch.Select(chunk => chunk.TextHash).ToList(), token);
        var vectors = new IReadOnlyList<float>[batch.Count];
        var chunksToEmbed = new List<int>(batch.Count);
        for (var index = 0; index < batch.Count; index++)
        {
            if (storedVectors[index] is { } storedVector)
                vectors[index] = storedVector;
            else
                chunksToEmbed.Add(index);
        }

        if (chunksToEmbed.Count > 0)
        {
            var embeddedVectors = await this.EmbedAsync(document, chunksToEmbed.Select(index => batch[index].Text).ToList(), token);
            for (var position = 0; position < chunksToEmbed.Count; position++)
                vectors[chunksToEmbed[position]] = embeddedVectors[position];
        }

        token.ThrowIfCancellationRequested();
        var embeddedAtUtc = DateTimeOffset.UtcNow;
        var state = document.State;
        var points = batch.Select((item, index) => new VectorStoragePoint(
            item.ChunkId,
            vectors[index],
            dataSource.Id,
            dataSource.Type.ToString(),
            item.ChunkId,
            state.ParentFileId,
            document.Key,
            state.AbsolutePath,
            state.FileName,
            state.RelativePath,
            state.FileType,
            item.PageNumber,
            item.EndPageNumber,
            item.ChunkIndex,
            item.Text,
            state.Fingerprint,
            state.CreationUtc,
            state.LastWriteUtc,
            embeddedAtUtc)).ToList();

        await vectorStore.InsertEmbedding(this.CollectionName, points, token);
        token.ThrowIfCancellationRequested();

        var chunks = batch
            .Select(chunk => new EmbeddingStateChunk(chunk.ChunkId, state.ParentFileId, chunk.PageNumber, chunk.EndPageNumber, chunk.ChunkIndex, chunk.Text, embeddedAtUtc, chunk.TextHash, reusableVectors.NextGeneration))
            .ToList();

        await indexStore.UpsertChunksAsync(dataSource.Id, chunks, token);
        await this.RecordStoredChunksAsync(batch.Count, token);

        logger.LogDebug(
            "Stored {ChunkCount} embedded chunks for file '{FilePath}' in collection '{CollectionName}'.",
            batch.Count,
            document.Key,
            this.CollectionName);

        var reusedChunkCount = batch.Count - chunksToEmbed.Count;
        batch.Clear();
        return reusedChunkCount;
    }

    /// <summary>
    /// Asks the embedding provider for the vectors of some texts, and checks them before anything is stored.
    /// </summary>
    /// <remarks>
    /// The first vectors a data source gets also decide the size of its store, which is created then.
    /// </remarks>
    /// <param name="document">The document the texts belong to.</param>
    /// <param name="texts">The texts.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>One vector per text, in the same order.</returns>
    private async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedAsync(EmbeddingDocument document, List<string> texts, CancellationToken token)
    {
        logger.LogDebug(
            "Requesting embeddings for batch of {ChunkCount} chunks from file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}).",
            texts.Count,
            document.Key,
            dataSource.Name,
            dataSource.Id);

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
            //
            // The messages of these exceptions end up in the log, never in front of the user, so
            // they name the document by its key: the name a user reads may be the subject of a mail.
            //
            throw new InvalidOperationException(string.Format(TB("The embedding provider was not able to embed {0} part(s) of '{1}'. The provider reported: {2}"), texts.Count, document.Key, exception.Message), exception);
        }

        if (vectors.Count != texts.Count)
            throw new InvalidOperationException(string.Format(TB("The embedding provider answered with {0} vectors for {1} parts of '{2}'. Please select another embedding model or provider."), vectors.Count, texts.Count, document.Key));

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
            var ensureResult = await vectorStore.EnsureVectorStoreExists(this.CollectionName, dataSource.Name, vectorSize, token);
            if (!ensureResult.Created)
            {
                logger.LogWarning(
                    "Vector store '{CollectionName}' exists for data source '{DataSourceName}' ({DataSourceId}) although no persisted embedding state exists. Replacing the orphaned store before indexing.",
                    this.CollectionName,
                    dataSource.Name,
                    dataSource.Id);
                await vectorStore.DeleteVectorStore(this.CollectionName, token);
                ensureResult = await vectorStore.EnsureVectorStoreExists(this.CollectionName, dataSource.Name, vectorSize, token);
                if (!ensureResult.Created)
                    throw new InvalidOperationException(string.Format(TB("The local index '{0}' could not be created again. Please restart AI Studio and try once more."), this.CollectionName));
            }

            await indexStore.UpdateVectorSizeAsync(dataSource.Id, vectorSize, token);
            manifest.VectorSize = vectorSize;
            logger.LogInformation(
                "Created embedding collection '{CollectionName}' with vector size {VectorSize} for data source '{DataSourceName}' ({DataSourceId}).",
                this.CollectionName,
                vectorSize,
                dataSource.Name,
                dataSource.Id);
        }

        return vectors;
    }
}
