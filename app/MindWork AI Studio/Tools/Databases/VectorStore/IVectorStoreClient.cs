namespace AIStudio.Tools.Databases.VectorStore;

public interface IVectorStoreClient
{
    Task EnsureVectorStoreExists(string storeName, int vectorSize, CancellationToken token);

    Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token);

    Task DeleteEmbeddingByFile(string storeName, string filePath, CancellationToken token);

    Task DeleteVectorStore(string storeName, CancellationToken token);
}
