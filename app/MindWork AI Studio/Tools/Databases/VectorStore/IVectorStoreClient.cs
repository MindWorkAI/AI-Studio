namespace AIStudio.Tools.Databases.VectorStore;

public interface IVectorStoreClient
{
    string Name { get; }

    DatabaseClientStatus Status { get; }

    bool IsAvailable { get; }

    IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo();

    Task EnsureVectorStoreExists(string storeName, int vectorSize, CancellationToken token);

    Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token);

    Task DeleteEmbeddingByFile(string storeName, string filePath, CancellationToken token);

    Task DeleteVectorStore(string storeName, CancellationToken token);
}
