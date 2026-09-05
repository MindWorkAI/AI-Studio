namespace AIStudio.Tools.Databases.VectorStore;

public abstract class VectorStoreClient(string name, string path): DatabaseClient(name, path)
{
    public abstract Task<VectorStoreEnsureResult> EnsureVectorStoreExists(string storeName, string dataSourceName, int vectorSize, CancellationToken token);

    public abstract Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token);

    public abstract Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, CancellationToken token);

    public abstract Task DeleteEmbeddingByFile(string storeName, string filePath, CancellationToken token);

    public abstract Task OptimizeVectorStore(string storeName, CancellationToken token);

    public abstract Task DeleteVectorStore(string storeName, CancellationToken token);
}
