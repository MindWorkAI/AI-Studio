namespace AIStudio.Tools.Databases.VectorStore;

public sealed record VectorStoragePoint(
    string PointId,
    IReadOnlyList<float> Vector,
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
    DateTimeOffset CreationUtc,
    DateTimeOffset LastWriteUtc,
    DateTimeOffset EmbeddedAtUtc,
    string ConfidenceLevel,
    int ConfidenceLevelRank);
