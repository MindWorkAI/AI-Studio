using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Databases;

public sealed class NoEmbeddingStore(string name, string? unavailableReason) : EmbeddingStore(name, string.Empty)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(NoEmbeddingStore).Namespace, nameof(NoEmbeddingStore));
    
    public override bool IsAvailable => false;

    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        yield return (TB("Status"), TB("Unavailable"));

        if (!string.IsNullOrWhiteSpace(unavailableReason))
            yield return (TB("Reason"), unavailableReason);

        await Task.CompletedTask;
    }

    public override Task EnsureEmbeddingStoreExists(string collectionName, int vectorSize, CancellationToken token) => throw this.BuildUnavailableException();

    public override Task InsertEmbedding(string collectionName, IReadOnlyList<EmbeddingStoragePoint> points, CancellationToken token) => throw this.BuildUnavailableException();

    public override Task DeleteEmbeddingByFile(string collectionName, string filePath, CancellationToken token) => Task.CompletedTask;

    public override Task DeleteEmbeddingStore(string collectionName, CancellationToken token) => Task.CompletedTask;

    public override void Dispose()
    {
    }

    private InvalidOperationException BuildUnavailableException()
    {
        return new InvalidOperationException(string.IsNullOrWhiteSpace(unavailableReason)
            ? "The vector database is not available."
            : unavailableReason);
    }
}
