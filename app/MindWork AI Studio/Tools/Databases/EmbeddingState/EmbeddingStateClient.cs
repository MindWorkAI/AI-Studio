using AIStudio.Tools.Services;

namespace AIStudio.Tools.Databases.EmbeddingState;

public abstract class EmbeddingStateClient(string name, string path) : DatabaseClient(name, path)
{
    public abstract Task<DataSourceEmbeddingManifest> GetManifestAsync(string dataSourceId, CancellationToken token);

    public abstract Task UpsertDataSourceAsync(
        string dataSourceId,
        string dataSourceName,
        string dataSourceType,
        string embeddingProviderId,
        string embeddingSignature,
        string sourceHash,
        int vectorSize,
        CancellationToken token);

    public abstract Task UpdateVectorSizeAsync(string dataSourceId, int vectorSize, CancellationToken token);

    public abstract Task UpdateDataSourceHashAsync(string dataSourceId, string sourceHash, CancellationToken token);

    public abstract Task UpsertFileAsync(string dataSourceId, EmbeddingStateFile file, CancellationToken token);

    public abstract Task DeleteFileAsync(string dataSourceId, string filePath, CancellationToken token);

    public abstract Task DeleteDataSourceAsync(string dataSourceId, CancellationToken token);
}

public sealed record EmbeddingStateFile(
    string FilePath,
    string FileName,
    string RelativePath,
    string Fingerprint,
    long FileSize,
    DateTime LastWriteUtc,
    DateTime EmbeddedAtUtc,
    int ChunkCount);
