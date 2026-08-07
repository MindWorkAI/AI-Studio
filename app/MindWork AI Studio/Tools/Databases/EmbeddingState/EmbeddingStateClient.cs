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

    public abstract Task UpsertChunksAsync(string dataSourceId, IReadOnlyList<EmbeddingStateChunk> chunks, CancellationToken token);

    public abstract Task<IReadOnlyList<EmbeddingStateSearchResult>> SearchChunksAsync(string dataSourceId, string query, int maxMatches, CancellationToken token);

    public abstract Task DeleteDataSourceAsync(string dataSourceId, CancellationToken token);
}

public sealed record EmbeddingStateFile(
    string ParentFileId,
    string AbsolutePath,
    string FileName,
    string RelativePath,
    string FileType,
    string Fingerprint,
    long FileSize,
    DateTime CreationUtc,
    DateTime LastWriteUtc,
    DateTime EmbeddedAtUtc,
    int ChunkCount,
    string ComplianceLevel,
    int ComplianceLevelRank);

public sealed record EmbeddingStateChunk(
    string ChunkId,
    string ParentFileId,
    int? PageNumber,
    int ChunkIndex,
    string ChunkText,
    DateTime EmbeddedAtUtc);

public sealed record EmbeddingStateSearchResult(
    string ChunkId,
    string ParentFileId,
    string DataSourceId,
    string DataSourceName,
    string DataSourceType,
    string AbsolutePath,
    string FileName,
    string RelativePath,
    string FileType,
    int? PageNumber,
    int ChunkIndex,
    string ChunkText,
    double Score,
    string Fingerprint,
    long FileSize,
    DateTime CreationUtc,
    DateTime LastWriteUtc,
    DateTime EmbeddedAtUtc,
    int ChunkCount,
    string ComplianceLevel,
    int ComplianceLevelRank);
