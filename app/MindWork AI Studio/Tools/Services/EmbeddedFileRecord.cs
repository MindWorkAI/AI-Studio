namespace AIStudio.Tools.Services;

public sealed record EmbeddedFileRecord(string Fingerprint, long FileSize, DateTimeOffset LastWriteUtc, DateTimeOffset EmbeddedAtUtc, int ChunkCount);
