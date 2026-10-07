namespace AIStudio.Tools.Databases.VectorStore;

public abstract class VectorStoreClient(string name, string path): DatabaseClient(name, path)
{
    public abstract Task<VectorStoreEnsureResult> EnsureVectorStoreExists(string storeName, string dataSourceName, int vectorSize, CancellationToken token);

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
}
