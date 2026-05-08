namespace AIStudio.Tools.Databases;

public sealed record EmbeddingStoragePoint(
    string PointId,
    IReadOnlyList<float> Vector,
    string DataSourceId,
    string DataSourceName,
    string DataSourceType,
    string FilePath,
    string FileName,
    string RelativePath,
    int ChunkIndex,
    string Text,
    string Fingerprint,
    DateTime LastWriteUtc,
    DateTime EmbeddedAtUtc);
