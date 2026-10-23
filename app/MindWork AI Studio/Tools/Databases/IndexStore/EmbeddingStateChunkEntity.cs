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

    /// <summary>
    /// The sheet the chunk is from, or null when its document has no sheets or the chunk was stored before v26.10.1.
    /// </summary>
    /// <remarks>
    /// A chunk never runs from one sheet into the next, cf. TextChunker.SplitBySheets. The location
    /// columns hold what the runtime states about the pieces of a document. A chunk stored before
    /// they existed keeps NULL in all of them, which reads as unknown, and gains them once its
    /// document is indexed again.
    /// </remarks>
    public string? SheetName { get; set; }

    /// <summary>
    /// The row of its sheet the chunk starts at, counted from one as a spreadsheet program counts it.
    /// </summary>
    public int? StartRow { get; set; }

    /// <summary>
    /// The row of its sheet the chunk ends at.
    /// </summary>
    public int? EndRow { get; set; }

    /// <summary>
    /// The line the chunk starts at, for text, CSV and source code, counted from one.
    /// </summary>
    public int? StartLine { get; set; }

    /// <summary>
    /// The line the chunk ends at.
    /// </summary>
    public int? EndLine { get; set; }

    /// <summary>
    /// Which attachment of a mail the chunk is from, as mail_parts numbers the attachments of a
    /// mail, or null for the text of the mail itself and for every chunk of a file.
    /// </summary>
    public int? AttachmentPosition { get; set; }

    public DateTimeOffset EmbeddedAtUtc { get; set; }

    public EmbeddingStateFileEntity? File { get; set; }
}
