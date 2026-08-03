namespace AIStudio.Tools.Databases.VectorStore;

public sealed record VectorSearchResult(
    string PointId,
    double Score,
    string DataSourceId,
    string DataSourceName,
    string DataSourceType,
    string ChunkId,
    string ParentFileId,
    string FilePath,
    string AbsolutePath,
    string FileName,
    string RelativePath,
    string FileType,
    int? PageNumber,
    int ChunkIndex,
    string Text,
    string Fingerprint,
    string CreationUtc,
    string LastWriteUtc,
    string EmbeddedAtUtc,
    string ComplianceLevel,
    int ComplianceLevelRank);
