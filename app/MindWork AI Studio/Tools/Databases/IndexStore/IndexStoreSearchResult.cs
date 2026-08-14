namespace AIStudio.Tools.Databases.IndexStore;

public sealed record IndexStoreSearchResult(
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
    DateTimeOffset CreationUtc,
    DateTimeOffset LastWriteUtc,
    DateTimeOffset EmbeddedAtUtc,
    int ChunkCount,
    string ConfidenceLevel,
    int ConfidenceLevelRank);
