namespace AIStudio.Tools.Databases.IndexStore;

internal sealed class EmbeddingStateChunkEntity
{
    public int Id { get; set; }

    public string ChunkId { get; set; } = string.Empty;

    public string ParentFileId { get; set; } = string.Empty;

    public int? PageNumber { get; set; }

    public int? EndPageNumber { get; set; }

    public int ChunkIndex { get; set; }

    public string ChunkText { get; set; } = string.Empty;

    /// <summary>
    /// The hash of the chunk text, cf. ChunkTextHash, or null for a chunk stored before v26.10.1.
    /// </summary>
    /// <remarks>
    /// Stored so that a document read again need not hash every old chunk text before it can tell
    /// which vectors to keep. A chunk without it is hashed from its text instead.
    /// </remarks>
    public string? TextHash { get; set; }

    /// <summary>
    /// How many times the document was indexed before this chunk was written, cf. IndexedDocumentIds.CreateChunkIdSource.
    /// </summary>
    /// <remarks>
    /// The chunk id includes it, so a document indexed again never overwrites its old chunks with
    /// new ones, even where its fingerprint stays the same, as a mail's does. Chunks stored before
    /// v26.10.1 are generation 0.
    /// </remarks>
    public int Generation { get; set; }

    public DateTimeOffset EmbeddedAtUtc { get; set; }

    public EmbeddingStateFileEntity? File { get; set; }
}
