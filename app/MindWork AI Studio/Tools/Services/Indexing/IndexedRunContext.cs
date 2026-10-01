using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;

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
/// <param name="logger">The logger of the embedding service, so the log reads the same whoever writes it.</param>
internal sealed class IndexedRunContext(IIndexedDataSource dataSource, EmbeddingProvider embeddingProvider, IProvider provider, VectorStoreClient vectorStore, IndexStoreClient indexStore, DataSourceEmbeddingManifest manifest, ILogger logger)
{
    /// <summary>
    /// After how many stored chunks the collection is optimized while a run is still going.
    /// </summary>
    private const int VECTOR_STORE_OPTIMIZATION_CHUNK_THRESHOLD = 100_000;

    private long storedChunksSinceLastOptimization;
    private bool hasPendingChanges;

    public IIndexedDataSource DataSource => dataSource;

    public EmbeddingProvider EmbeddingProvider => embeddingProvider;

    public IProvider Provider => provider;

    public VectorStoreClient VectorStore => vectorStore;

    public IndexStoreClient IndexStore => indexStore;

    public DataSourceEmbeddingManifest Manifest => manifest;

    public string CollectionName { get; } = DataSourceEmbeddingNames.GetCollectionName(dataSource.Id);

    /// <summary>
    /// Removes the vectors of one document from the collection.
    /// </summary>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task DeleteDocumentPointsAsync(string documentKey, CancellationToken token)
    {
        await vectorStore.DeleteEmbeddingByFile(this.CollectionName, documentKey, token);
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
}