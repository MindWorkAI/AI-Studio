namespace AIStudio.Tools.Databases.IndexStore;

public sealed record EmbeddingStateFile(
    string ParentFileId,
    string AbsolutePath,
    string FileName,
    string RelativePath,
    string FileType,
    string Fingerprint,
    long FileSize,
    DateTimeOffset CreationUtc,
    DateTimeOffset LastWriteUtc,
    DateTimeOffset EmbeddedAtUtc,
    int ChunkCount,
    string? TranscriptKey = null,
    string? ContentHash = null,
    int Generation = 0);
