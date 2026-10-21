using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Databases.VectorStore;

public sealed class NoVectorStoreClient(string name, string? unavailableReason, DatabaseClientStatus status = DatabaseClientStatus.UNAVAILABLE) : VectorStoreClient(name, string.Empty)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(NoVectorStoreClient).Namespace, nameof(NoVectorStoreClient));

    public override DatabaseClientStatus Status => status;

    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        yield return (TB("Status"), status switch
        {
            DatabaseClientStatus.STARTING => TB("Starting"),
            _ => TB("Unavailable")
        });

        if (!string.IsNullOrWhiteSpace(unavailableReason))
            yield return (TB("Reason"), unavailableReason);

        await Task.CompletedTask;
    }

    public override Task<VectorStoreEnsureResult> EnsureVectorStoreExists(string storeName, string dataSourceName, int vectorSize, CancellationToken token) =>
        Task.FromException<VectorStoreEnsureResult>(this.CreateUnavailableException());

    public override Task<bool> VectorStoreExists(string storeName, CancellationToken token) =>
        Task.FromException<bool>(this.CreateUnavailableException());

    public override Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token) =>
        Task.FromException(this.CreateUnavailableException());

    public override Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, CancellationToken token) =>
        Task.FromException<IReadOnlyList<VectorSearchResult>>(this.CreateUnavailableException());

    public override Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, VectorSearchFilter filter, CancellationToken token) =>
        Task.FromException<IReadOnlyList<VectorSearchResult>>(this.CreateUnavailableException());

    public override Task DeleteEmbeddingByFile(string storeName, string filePath, string parentFileId, CancellationToken token) =>
        Task.FromException(this.CreateUnavailableException());

    public override Task RelocateEmbeddingFileAsync(string storeName, string parentFileId, string absolutePath, string relativePath, string fingerprint, CancellationToken token) =>
        Task.FromException(this.CreateUnavailableException());

    public override Task OptimizeVectorStore(string storeName, CancellationToken token) =>
        Task.FromException(this.CreateUnavailableException());

    public override Task DeleteVectorStore(string storeName, CancellationToken token) =>
        Task.FromException(this.CreateUnavailableException());

    public override Task<VectorStoreBackupPreparation> PrepareBackupAsync(CancellationToken token) =>
        Task.FromException<VectorStoreBackupPreparation>(this.CreateUnavailableException());

    public override Task<long> CopyStoreForBackupAsync(string backupId, string storeName, CancellationToken token) =>
        Task.FromException<long>(this.CreateUnavailableException());

    private InvalidOperationException CreateUnavailableException() =>
        new(unavailableReason ?? "The vector store is not available.");

    public override void Dispose()
    {
    }
}
