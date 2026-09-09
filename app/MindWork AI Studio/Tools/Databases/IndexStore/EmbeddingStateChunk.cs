namespace AIStudio.Tools.Databases.IndexStore;

public sealed record EmbeddingStateChunk(string ChunkId, string ParentFileId, int? PageNumber, int ChunkIndex, string ChunkText, DateTimeOffset EmbeddedAtUtc);
