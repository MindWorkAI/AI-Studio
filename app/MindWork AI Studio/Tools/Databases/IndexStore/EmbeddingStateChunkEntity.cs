namespace AIStudio.Tools.Databases.IndexStore;

internal sealed class EmbeddingStateChunkEntity
{
    public int Id { get; set; }

    public string ChunkId { get; set; } = string.Empty;

    public string ParentFileId { get; set; } = string.Empty;

    public int? PageNumber { get; set; }

    public int ChunkIndex { get; set; }

    public string ChunkText { get; set; } = string.Empty;

    public DateTimeOffset EmbeddedAtUtc { get; set; }

    public EmbeddingStateFileEntity? File { get; set; }
}
