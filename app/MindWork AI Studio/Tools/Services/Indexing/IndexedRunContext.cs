using System.Runtime.ExceptionServices;

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

    private readonly Dictionary<string, IndexingAttempt> attempts = new(DataSourceEmbeddingManifest.PATH_COMPARER);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(IndexedRunContext).Namespace, nameof(IndexedRunContext));

    /// <summary>
    /// One chunk on its way to the stores, with the id it is stored under and the hash of its text.
    /// </summary>
    private sealed record EmbeddingChunkDraft(string ChunkId, EmbeddingChunk Chunk, string TextHash, int ChunkIndex)
    {
        public string Text => this.Chunk.Text;
    }

    /// <summary>
    /// One attempt to index a document, from reading what it had stored until it is completed or cleaned up.
    /// </summary>
    /// <param name="generation">The generation the attempt writes.</param>
    /// <param name="earlierGenerations">The generations the document had stored when the attempt began.</param>
    private sealed class IndexingAttempt(int generation, IReadOnlyCollection<int> earlierGenerations)
    {
        public int Generation => generation;

        public IReadOnlyCollection<int> EarlierGenerations => earlierGenerations;

        /// <summary>
        /// The first failure of the embedding provider, after which the attempt asks it nothing more.
        /// </summary>
        public Exception? ProviderFailure { get; set; }

        /// <summary>
        /// Whether the attempt holds every stored vector the document still needs, so it stays when it fails, cf. CleanupFailedDocumentAsync.
        /// </summary>
        /// <remarks>
        /// True once the attempt got through the whole document, even when the embedding provider
        /// failed on the way: each chunk whose text the document had stored in any generation took
        /// over that vector. True as well as soon as the provider fails for a document which had
        /// nothing stored: no chunk after that could take over a vector, so the attempt stops there.
        /// </remarks>
        public bool HoldsEveryStoredVector { get; set; }
    }

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
    /// Embeds one document and stores its chunks as a new generation, next to whatever it had stored before.
    /// </summary>
    /// <remarks>
    /// Which chunks the document has stored is read first. Each chunk whose text the document had
    /// before takes over the stored vector instead of going to the embedding provider again. The
    /// new chunks belong to the next generation and have ids of their own, so none of them
    /// overwrites an old one.<br/><br/>
    /// A document indexed before keeps its row, its chunks and its vectors meanwhile: the new
    /// generation replaces them only once whoever indexes records the document as indexed, cf.
    /// CompleteDocumentAsync, since only the kind of data source knows whether the document changed
    /// in the meantime. Until then, no search finds the new generation, cf.
    /// IndexStoreClient.GetHiddenChunksAsync. A document indexed for the first time gets a row with
    /// a chunk count of zero, so its chunks have something to point at while they arrive.<br/><br/>
    /// Once the embedding provider fails, it is asked nothing more for this document, and the rest
    /// of the document is read only for the chunks which can take over a stored vector. So the
    /// attempt ends up with every stored vector the document still needs, and stays next to the
    /// stored version when it is cleaned up, cf. CleanupFailedDocumentAsync. The failure of the
    /// provider is thrown at the end, as it was. A document which had nothing stored stops right
    /// at the failure instead, since none of its chunks could take over a vector. Every other
    /// failure is thrown right away.
    /// </remarks>
    /// <param name="document">The document to index.</param>
    /// <param name="reportBlockProgress">Told about every chunk, with its number and its page.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>What was stored for the document.</returns>
    public async Task<IndexedDocument> IndexDocumentAsync(EmbeddingDocument document, Action<int, int?> reportBlockProgress, CancellationToken token)
    {
        var reusableVectors = await ReusableVectors.ReadAsync(indexStore, dataSource.Id, document.Key, token);
        var attempt = new IndexingAttempt(reusableVectors.NextGeneration, reusableVectors.StoredGenerations);
        this.attempts[document.Key] = attempt;

        logger.LogDebug(
            "Indexing file '{FilePath}' in collection '{CollectionName}' as generation {Generation}. What it had stored stays until the new chunks are complete.",
            document.Key,
            this.CollectionName,
            attempt.Generation);

        //
        // A document which was never indexed completely gets its row now, whose chunk count of zero
        // hides it from every search. An attempt it kept before stays under the same row, so this one
        // reuses its vectors, and a failure leaves them in place, cf. CleanupFailedDocumentAsync:
        //
        if (!manifest.Files.ContainsKey(document.Key))
            await indexStore.UpsertFileAsync(dataSource.Id, document.State with { Generation = attempt.Generation }, token);

        var embeddingBatchSize = Math.Max(1, embeddingProvider.EffectiveEmbeddingBatchSize);
        var batch = new List<EmbeddingChunkDraft>(embeddingBatchSize);
        var totalChunkCount = 0;
        var storedChunkCount = 0;
        var reusedChunkCount = 0;
        var storedChunkIds = new List<string>();
        var chunkIdSource = IndexedDocumentIds.CreateChunkIdSource(dataSource.Id, document.Key, document.State.ParentFileId, document.State.Fingerprint, attempt.Generation);

        await foreach (var chunk in document.StreamChunks(token))
        {
            var chunkId = IndexedDocumentIds.CreateChunkId(dataSource.Id, chunkIdSource, totalChunkCount);
            batch.Add(new(chunkId, chunk, ChunkTextHash.Of(chunk.Text), totalChunkCount));
            storedChunkIds.Add(chunkId);
            totalChunkCount++;
            reportBlockProgress(totalChunkCount, chunk.PageNumber);

            if (batch.Count >= embeddingBatchSize)
            {
                var (stored, reused) = await this.FlushBatchAsync(document, batch, reusableVectors, attempt, token);
                storedChunkCount += stored;
                reusedChunkCount += reused;
            }
        }

        if (batch.Count > 0)
        {
            var (stored, reused) = await this.FlushBatchAsync(document, batch, reusableVectors, attempt, token);
            storedChunkCount += stored;
            reusedChunkCount += reused;
        }

        //
        // The extraction itself did not report a failure, but nothing usable came out of it. For
        // the index this is the same case as a scanned page without a text layer, which is why it
        // carries a code of its own instead of an unclassified exception:
        //
        if (totalChunkCount == 0)
            throw new FileExtractionException(FileExtractionErrorCode.NO_CONTENT, string.Format(TB("No text could be read from '{0}'."), document.Key));

        attempt.HoldsEveryStoredVector = true;
        if (attempt.ProviderFailure is not null)
        {
            logger.LogInformation(
                "Read file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}) to its end without asking the failed embedding provider again. {StoredChunkCount} of {ChunkCount} chunks were stored with a vector, and stay for the next attempt.",
                document.Key,
                dataSource.Name,
                dataSource.Id,
                storedChunkCount,
                totalChunkCount);

            ExceptionDispatchInfo.Throw(attempt.ProviderFailure);
        }

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

        return new(totalChunkCount, attempt.Generation, storedChunkIds);
    }

    /// <summary>
    /// Completes a document indexed again: its new generation stays, and every other one goes, in both stores.
    /// </summary>
    /// <remarks>
    /// Called by whoever records the document as indexed, cf. DocumentRunProgress.RecordDocumentIndexedAsync,
    /// right after its row names the new generation. From then on, no search finds the other
    /// generations, cf. IndexStoreClient.GetHiddenChunksAsync, so a store which refuses to remove
    /// them costs space, not the document. The next indexing of the document removes them then.
    /// Whatever an interrupted run left behind for the document goes as well, since it belongs to
    /// no generation the document keeps.
    /// </remarks>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="indexed">What indexing the document stored.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task CompleteDocumentAsync(string documentKey, IndexedDocument indexed, CancellationToken token)
    {
        this.attempts.Remove(documentKey);
        try
        {
            await this.KeepGenerationsAsync(documentKey, [indexed.Generation], indexed.ChunkIds, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not remove the earlier generations of file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}) after it was indexed. No search finds them, and the next indexing of the file removes them.",
                documentKey,
                dataSource.Name,
                dataSource.Id);
        }
    }

    /// <summary>
    /// Removes what failed attempts and interrupted runs left behind of documents which are complete and did not change since.
    /// </summary>
    /// <remarks>
    /// The next indexing of a document removes such generations, cf. CompleteDocumentAsync. A
    /// document which did not change since is not indexed again, though, e.g., a file whose edit was
    /// undone, or one whose run stopped while removing them, so nothing else would ever remove
    /// them. No search finds them, so they cost space only, and a failure here costs the run nothing.
    /// </remarks>
    /// <param name="isUnchanged">Whether the document with the given key is the same as what its row describes.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task RemoveLeftoverGenerationsAsync(Func<string, bool> isUnchanged, CancellationToken token)
    {
        try
        {
            // The documents with hidden chunks, which are few. A document whose first indexing is not complete has none to search by, and stays as it is:
            var documents = await indexStore.GetHiddenChunksAsync(dataSource.Id, token);
            if (documents.Count is 0)
                return;

            var keysByParentId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, record) in manifest.Files)
                if (record.ParentFileId is { } parentFileId)
                    keysByParentId.TryAdd(parentFileId, key);

            foreach (var document in documents)
            {
                token.ThrowIfCancellationRequested();
                if (document.SearchableGeneration is { } generation && keysByParentId.TryGetValue(document.ParentFileId, out var key) && manifest.Files[key].Generation == generation && isUnchanged(key))
                    await this.KeepOnlyGenerationAsync(key, generation, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not remove what earlier runs left behind of the documents in data source '{DataSourceName}' ({DataSourceId}). No search finds it, and the next run tries again.", dataSource.Name, dataSource.Id);
        }
    }

    /// <summary>
    /// Removes every generation of a document but the given one, when it has any other.
    /// </summary>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="generation">The generation which stays, the one its row names.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task KeepOnlyGenerationAsync(string documentKey, int generation, CancellationToken token)
    {
        var storedChunks = await indexStore.GetStoredChunksAsync(dataSource.Id, documentKey, token);
        if (storedChunks.All(chunk => chunk.Generation == generation))
            return;

        await this.KeepGenerationsAsync(documentKey, [generation], storedChunks.Where(chunk => chunk.Generation == generation).Select(chunk => chunk.ChunkId).ToList(), token);
    }

    /// <summary>
    /// Removes every generation of a document but the given ones, from the vector store first.
    /// </summary>
    /// <remarks>
    /// The index store forgets a chunk only once its point is gone. As long as it knows a chunk, no
    /// search finds the chunk unless it belongs to the generation the document is searched by, cf.
    /// IndexStoreClient.GetHiddenChunksAsync. A point the index store no longer knew of could be
    /// found by the vector search.
    /// </remarks>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="generations">The generations which stay.</param>
    /// <param name="chunkIds">The ids of their chunks, which are the points that stay.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task KeepGenerationsAsync(string documentKey, IReadOnlyCollection<int> generations, IReadOnlyCollection<string> chunkIds, CancellationToken token)
    {
        await this.DeleteDocumentPointsAsync(documentKey, chunkIds, token);
        await indexStore.DeleteOtherGenerationsAsync(dataSource.Id, documentKey, generations, token);
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
    /// Sorts out what a failed attempt left behind of one document.
    /// </summary>
    /// <remarks>
    /// A document indexed before keeps the version it had. That holds for every failure, one which
    /// lies in the document included: a file broken by an edit may be fixed again, and meanwhile
    /// its last good version can still be found.<br/><br/>
    /// An attempt which holds every stored vector the document still needs stays as well, though
    /// no search finds it, cf. IndexStoreClient.GetHiddenChunksAsync: one which got through the
    /// whole document, or one of a document which had nothing stored when the embedding provider
    /// failed. The vectors it got from the provider before the failure are among them, so the next
    /// attempt asks the provider only for the rest. Every other generation goes, so a document
    /// keeps two at most. Any other attempt goes instead, and what the document had stored before
    /// it stays as it was.<br/><br/>
    /// A document indexed for the first time has no version to keep. It keeps an attempt which
    /// holds its vectors, under a row whose chunk count stays zero. Otherwise, it keeps what an
    /// earlier attempt kept, and loses everything when there is none.<br/><br/>
    /// Never throws for a store which refuses: the document already failed, and that failure is the
    /// one the user has to hear about.
    /// </remarks>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task CleanupFailedDocumentAsync(string documentKey, CancellationToken token)
    {
        var storedVersion = manifest.Files.GetValueOrDefault(documentKey);
        this.attempts.Remove(documentKey, out var attempt);

        // The attempt failed before it wrote anything, so what the document had stays as it was:
        if (attempt is null)
            return;

        IReadOnlyCollection<int> keptGenerations = (storedVersion, attempt) switch
        {
            (null, { HoldsEveryStoredVector: true } kept) => [kept.Generation],
            ({ } stored, { HoldsEveryStoredVector: true } kept) => [stored.Generation, kept.Generation],
            _ => attempt.EarlierGenerations,
        };

        try
        {
            IReadOnlyList<string> keptChunkIds = keptGenerations.Count is 0 ? [] : await indexStore.GetChunkIdsAsync(dataSource.Id, documentKey, keptGenerations, token);
            if (storedVersion is null && keptChunkIds.Count is 0)
            {
                await this.DeleteDocumentPointsAsync(documentKey, token);
                await indexStore.DeleteFileAsync(dataSource.Id, documentKey, token);
                manifest.UnfinishedFiles.Remove(documentKey);
                return;
            }

            await this.KeepGenerationsAsync(documentKey, keptGenerations, keptChunkIds, token);
            if (storedVersion is null)
                manifest.UnfinishedFiles.Add(documentKey);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not remove what a failed attempt left behind of file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}). No search finds it, and the next attempt removes it.",
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
    /// Stores one batch of chunks, each with its vector, and leaves out the chunks without one.
    /// </summary>
    /// <remarks>
    /// A chunk goes without a vector only once the embedding provider failed for the document, cf.
    /// IndexDocumentAsync.
    /// </remarks>
    /// <param name="document">The document the chunks belong to.</param>
    /// <param name="batch">The chunks, emptied once they are stored.</param>
    /// <param name="reusableVectors">The chunks the document had stored before.</param>
    /// <param name="attempt">The attempt the chunks belong to, which remembers whether the embedding provider failed.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>How many of the chunks were stored, and how many of those kept a stored vector instead of being embedded.</returns>
    private async Task<(int StoredCount, int ReusedCount)> FlushBatchAsync(EmbeddingDocument document, List<EmbeddingChunkDraft> batch, ReusableVectors reusableVectors, IndexingAttempt attempt, CancellationToken token)
    {
        //
        // A chunk whose text the document had stored before takes that vector over. Only the other
        // chunks go to the embedding provider:
        //
        var storedVectors = await reusableVectors.TakeAsync(vectorStore, this.CollectionName, manifest.VectorSize, batch.Select(chunk => chunk.TextHash).ToList(), token);
        var vectors = new IReadOnlyList<float>?[batch.Count];
        var chunksToEmbed = new List<int>(batch.Count);
        for (var index = 0; index < batch.Count; index++)
        {
            if (storedVectors[index] is { } storedVector)
                vectors[index] = storedVector;
            else
                chunksToEmbed.Add(index);
        }

        var reusedChunkCount = batch.Count - chunksToEmbed.Count;
        if (chunksToEmbed.Count > 0 && attempt.ProviderFailure is null)
        {
            IReadOnlyList<IReadOnlyList<float>>? embeddedVectors = null;
            try
            {
                embeddedVectors = await this.RequestVectorsAsync(document, chunksToEmbed.Select(index => batch[index].Text).ToList(), token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
            {
                //
                // A document which had nothing stored has no chunk which could take over a vector,
                // so reading on would only cost time. What the attempt embedded so far stays:
                //
                if (attempt.EarlierGenerations.Count is 0)
                {
                    attempt.HoldsEveryStoredVector = true;
                    throw;
                }

                //
                // The provider is asked nothing more for this document. It may be down, and once
                // the cause is fixed, e.g., a chunk too long for it, the chunks after that one may
                // be cut differently, so whatever it embedded meanwhile would be paid for nothing.
                // The rest of the document is still read, for the chunks with a stored vector:
                //
                attempt.ProviderFailure = exception;
                logger.LogDebug(
                    "The embedding provider failed for file '{FilePath}' in data source '{DataSourceName}' ({DataSourceId}). The rest of the file is read for the chunks with a stored vector only.",
                    document.Key,
                    dataSource.Name,
                    dataSource.Id);
            }

            if (embeddedVectors is not null)
            {
                await this.EnsureCollectionAsync(embeddedVectors[0].Count, token);
                for (var position = 0; position < chunksToEmbed.Count; position++)
                    vectors[chunksToEmbed[position]] = embeddedVectors[position];
            }
        }

        var chunksToStore = Enumerable.Range(0, batch.Count).Where(index => vectors[index] is not null).ToList();
        if (chunksToStore.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var embeddedAtUtc = DateTimeOffset.UtcNow;
            var state = document.State;
            var points = chunksToStore.Select(index => new VectorStoragePoint(
                batch[index].ChunkId,
                vectors[index]!,
                dataSource.Id,
                dataSource.Type.ToString(),
                batch[index].ChunkId,
                state.ParentFileId,
                document.Key,
                state.AbsolutePath,
                state.FileName,
                state.RelativePath,
                state.FileType,
                batch[index].Chunk.PageNumber,
                batch[index].Chunk.EndPageNumber,
                batch[index].ChunkIndex,
                batch[index].Text,
                state.Fingerprint,
                state.CreationUtc,
                state.LastWriteUtc,
                embeddedAtUtc,
                attempt.Generation)).ToList();

            //
            // The vector store keeps the pages only, cf. VectorStoragePoint. Everything else about
            // where a chunk is goes into the index store alone:
            //
            var chunks = chunksToStore
                .Select(index => batch[index])
                .Select(draft => new EmbeddingStateChunk(
                    draft.ChunkId,
                    state.ParentFileId,
                    draft.Chunk.PageNumber,
                    draft.Chunk.EndPageNumber,
                    draft.ChunkIndex,
                    draft.Text,
                    embeddedAtUtc,
                    draft.TextHash,
                    attempt.Generation,
                    draft.Chunk.Start.SheetName,
                    draft.Chunk.Start.RowNumber,
                    draft.Chunk.End.RowNumber,
                    draft.Chunk.Start.LineNumber,
                    draft.Chunk.End.LineNumber,
                    draft.Chunk.AttachmentPosition))
                .ToList();

            //
            // The index store first: as long as it knows the chunks of the new generation, no search
            // finds them, cf. IndexStoreClient.GetHiddenChunksAsync. A point it did not know yet, e.g.,
            // after the app stopped right here, would be found next to the stored version:
            //
            await indexStore.UpsertChunksAsync(dataSource.Id, chunks, token);
            token.ThrowIfCancellationRequested();

            await vectorStore.InsertEmbedding(this.CollectionName, points, token);
            await this.RecordStoredChunksAsync(chunksToStore.Count, token);

            logger.LogDebug(
                "Stored {ChunkCount} embedded chunks for file '{FilePath}' in collection '{CollectionName}'.",
                chunksToStore.Count,
                document.Key,
                this.CollectionName);
        }

        batch.Clear();
        return (chunksToStore.Count, reusedChunkCount);
    }

    /// <summary>
    /// Asks the embedding provider for the vectors of some texts, and checks them before anything is stored.
    /// </summary>
    /// <param name="document">The document the texts belong to.</param>
    /// <param name="texts">The texts.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>One vector per text, in the same order.</returns>
    private async Task<IReadOnlyList<IReadOnlyList<float>>> RequestVectorsAsync(EmbeddingDocument document, List<string> texts, CancellationToken token)
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

        return vectors;
    }

    /// <summary>
    /// Creates the store of the data source with the first vectors it gets, which decide its size.
    /// </summary>
    /// <param name="vectorSize">The size of the vectors the embedding provider answered with.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task EnsureCollectionAsync(int vectorSize, CancellationToken token)
    {
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
    }
}
