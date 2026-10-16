namespace AIStudio.Tools.Databases.VectorStore;

public abstract class VectorStoreClient(string name, string path): DatabaseClient(name, path)
{
    public abstract Task<VectorStoreEnsureResult> EnsureVectorStoreExists(string storeName, string dataSourceName, int vectorSize, CancellationToken token);

    /// <summary>
    /// Whether the store was created completely, without creating or opening it.
    /// </summary>
    /// <param name="storeName">The name of the store.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when the store exists.</returns>
    public abstract Task<bool> VectorStoreExists(string storeName, CancellationToken token);

    public abstract Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token);

    public abstract Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, CancellationToken token);

    /// <summary>
    /// Searches only those points of a store which pass the filter.
    /// </summary>
    /// <param name="storeName">The name of the store.</param>
    /// <param name="vector">The vector to search for.</param>
    /// <param name="maxMatches">How many matches to return at most.</param>
    /// <param name="filter">The points the search may return. Without any point, it finds nothing.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The matches, best first, all of them inside the filter.</returns>
    public abstract Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, VectorSearchFilter filter, CancellationToken token);

    public abstract Task DeleteEmbeddingByFile(string storeName, string filePath, CancellationToken token);

    public abstract Task OptimizeVectorStore(string storeName, CancellationToken token);

    public abstract Task DeleteVectorStore(string storeName, CancellationToken token);

    /// <summary>
    /// Prepares a backup of the stores: lists them, checks that their copies fit on the disk, and
    /// creates the directory for the copies below the data directory.
    /// </summary>
    /// <remarks>
    /// Pause the indexing first, cf. DataSourceEmbeddingService.PauseIndexingAsync, so that no store
    /// changes until all of them are copied.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The backup, with every store to copy.</returns>
    public abstract Task<VectorStoreBackupPreparation> PrepareBackupAsync(CancellationToken token);

    /// <summary>
    /// Copies one store into the directory of a prepared backup.
    /// </summary>
    /// <remarks>
    /// The store is unloaded for the copy and loads again with the next request. Requests for any
    /// store wait while one is copied.
    /// </remarks>
    /// <param name="backupId">The backup, as PrepareBackupAsync named it.</param>
    /// <param name="storeName">The name of the store.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>How many bytes were copied. Zero when the store was not created completely.</returns>
    public abstract Task<long> CopyStoreForBackupAsync(string backupId, string storeName, CancellationToken token);
}
