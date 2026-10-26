namespace AIStudio.Tools.Services;

public sealed record EmbeddedFileRecord(string Fingerprint, long FileSize, DateTimeOffset LastWriteUtc, DateTimeOffset EmbeddedAtUtc, int ChunkCount, string? TranscriptKey = null,
    string? RelativePath = null, string? ParentFileId = null, string? ContentHash = null, int Generation = 0);
