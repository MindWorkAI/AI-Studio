using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Databases.VectorStore;

public sealed class NoVectorStoreClient(string name, string? unavailableReason, DatabaseClientStatus status = DatabaseClientStatus.UNAVAILABLE) : IVectorStoreClient
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(NoVectorStoreClient).Namespace, nameof(NoVectorStoreClient));

    public string Name => name;

    public DatabaseClientStatus Status => status;

    public bool IsAvailable => false;

    public async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        yield return (TB("Status"), TB("Unavailable"));

        if (!string.IsNullOrWhiteSpace(unavailableReason))
            yield return (TB("Reason"), unavailableReason);

        await Task.CompletedTask;
    }

    public Task EnsureVectorStoreExists(string storeName, int vectorSize, CancellationToken token) =>
        Task.FromException(this.CreateUnavailableException());

    public Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token) =>
        Task.FromException(this.CreateUnavailableException());

    public Task DeleteEmbeddingByFile(string storeName, string filePath, CancellationToken token) =>
        Task.FromException(this.CreateUnavailableException());

    public Task DeleteVectorStore(string storeName, CancellationToken token) =>
        Task.FromException(this.CreateUnavailableException());

    private InvalidOperationException CreateUnavailableException() =>
        new(unavailableReason ?? "The vector store is not available.");
}
