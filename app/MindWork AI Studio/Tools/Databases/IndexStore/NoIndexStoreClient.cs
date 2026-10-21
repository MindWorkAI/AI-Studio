using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Databases.IndexStore;

public sealed partial class NoIndexStoreClient(string name, string? unavailableReason, DatabaseClientStatus status = DatabaseClientStatus.UNAVAILABLE) : IndexStoreClient(name, string.Empty)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(NoIndexStoreClient).Namespace, nameof(NoIndexStoreClient));

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

        //
        // Say which native library this process bound to even though the database itself is out of
        // reach. When SQLite cannot be loaded on a platform at all, this client is exactly what the
        // user sees, so this is the one place where those details matter most.
        //
        yield return (TB("Native library"), OrUnknown(SqliteRuntimeInfo.GetNativeLibraryName()));
        yield return (TB("Process architecture"), OrUnknown(SqliteRuntimeInfo.GetProcessArchitecture()));

        await Task.CompletedTask;
    }

    private static string OrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? TB("unknown") : value;

    public override Task<DataSourceEmbeddingManifest> GetManifestAsync(string dataSourceId, CancellationToken token) => Task.FromResult(new DataSourceEmbeddingManifest());

    public override Task<DataSourceIndexState?> GetDataSourceStateAsync(string dataSourceId, CancellationToken token) => Task.FromResult<DataSourceIndexState?>(null);

    public override Task UpsertDataSourceAsync(
        string dataSourceId,
        string dataSourceType,
        string embeddingProviderId,
        string embeddingSignature,
        string sourceHash,
        int vectorSize,
        CancellationToken token) => Task.CompletedTask;

    public override Task UpdateVectorSizeAsync(string dataSourceId, int vectorSize, CancellationToken token) => Task.CompletedTask;

    public override Task UpdateDataSourceHashAsync(string dataSourceId, string sourceHash, CancellationToken token) => Task.CompletedTask;

    public override Task UpsertFileAsync(string dataSourceId, EmbeddingStateFile file, CancellationToken token) => Task.CompletedTask;

    public override Task RelocateFileAsync(string dataSourceId, string parentFileId, string absolutePath, string relativePath, string fingerprint, CancellationToken token) =>
        Task.FromException(new InvalidOperationException(unavailableReason ?? "The index of the data sources is not available."));

    public override Task DeleteFileAsync(string dataSourceId, string filePath, CancellationToken token) => Task.CompletedTask;

    public override Task UpsertPermanentFailureAsync(string dataSourceId, PermanentIndexingFailure failure, CancellationToken token) => Task.CompletedTask;

    public override Task DeletePermanentFailureAsync(string dataSourceId, string filePath, CancellationToken token) => Task.CompletedTask;

    public override Task UpsertChunksAsync(string dataSourceId, IReadOnlyList<EmbeddingStateChunk> chunks, CancellationToken token) => Task.CompletedTask;

    public override Task<IReadOnlyList<IndexStoreSearchResult>> SearchChunksAsync(string dataSourceId, string query, int maxMatches, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<IndexStoreSearchResult>>([]);

    public override Task DeleteDataSourceAsync(string dataSourceId, CancellationToken token) => Task.CompletedTask;

    public override Task<long?> GetTotalChunkCountAsync(CancellationToken token) => Task.FromResult<long?>(null);

    /// <inheritdoc />
    /// <remarks>
    /// Unlike the other methods, this one does not pretend success. A backup without the index would
    /// still take the vector stores along, and after a restore, they would belong to nothing the index
    /// knows. Whoever makes the backup decides what to do instead.
    /// </remarks>
    public override Task<string> BackupToDirectoryAsync(string targetDirectory, CancellationToken token) =>
        Task.FromException<string>(new InvalidOperationException(unavailableReason ?? "The index of the data sources is not available."));

    public override void Dispose()
    {
    }
}
