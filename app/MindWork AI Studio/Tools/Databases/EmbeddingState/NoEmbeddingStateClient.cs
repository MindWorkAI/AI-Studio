using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Databases.EmbeddingState;

public sealed class NoEmbeddingStateClient(string name, string? unavailableReason, DatabaseClientStatus status = DatabaseClientStatus.UNAVAILABLE) : EmbeddingStateClient(name, string.Empty)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(NoEmbeddingStateClient).Namespace, nameof(NoEmbeddingStateClient));

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

    public override Task<DataSourceEmbeddingManifest> GetManifestAsync(string dataSourceId, CancellationToken token) =>
        Task.FromResult(new DataSourceEmbeddingManifest());

    public override Task UpsertDataSourceAsync(
        string dataSourceId,
        string dataSourceName,
        string dataSourceType,
        string embeddingProviderId,
        string embeddingSignature,
        string sourceHash,
        int vectorSize,
        CancellationToken token) => Task.CompletedTask;

    public override Task UpdateVectorSizeAsync(string dataSourceId, int vectorSize, CancellationToken token) => Task.CompletedTask;

    public override Task UpdateDataSourceHashAsync(string dataSourceId, string sourceHash, CancellationToken token) => Task.CompletedTask;

    public override Task UpsertFileAsync(string dataSourceId, EmbeddingStateFile file, CancellationToken token) => Task.CompletedTask;

    public override Task DeleteFileAsync(string dataSourceId, string filePath, CancellationToken token) => Task.CompletedTask;

    public override Task UpsertChunksAsync(string dataSourceId, IReadOnlyList<EmbeddingStateChunk> chunks, CancellationToken token) => Task.CompletedTask;

    public override Task<IReadOnlyList<EmbeddingStateSearchResult>> SearchChunksAsync(string dataSourceId, string query, int maxMatches, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<EmbeddingStateSearchResult>>([]);

    public override Task DeleteDataSourceAsync(string dataSourceId, CancellationToken token) => Task.CompletedTask;

    public override void Dispose()
    {
    }
}
