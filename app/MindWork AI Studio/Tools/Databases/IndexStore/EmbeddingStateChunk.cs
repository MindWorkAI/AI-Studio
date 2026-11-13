namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// One chunk as the index stores it.
/// </summary>
/// <remarks>
/// Where a chunk is goes into the typed columns its document has: pages, sheet and rows, or lines.
/// The vector store keeps only the pages, cf. VectorStoragePoint: a search completes its hits from
/// here by their chunk id.
/// </remarks>
/// <param name="ChunkId">The id of the chunk, which is the id of its point in the vector store.</param>
/// <param name="ParentFileId">The document the chunk belongs to.</param>
/// <param name="PageNumber">The page the chunk starts on, or null.</param>
/// <param name="EndPageNumber">The page the chunk ends on, or null.</param>
/// <param name="ChunkIndex">The place of the chunk in its document, counted from zero.</param>
/// <param name="ChunkText">The text which was embedded.</param>
/// <param name="EmbeddedAtUtc">When the chunk was stored.</param>
/// <param name="TextHash">The hash of the text, cf. ChunkTextHash.</param>
/// <param name="Generation">Which indexing of the document wrote the chunk.</param>
/// <param name="SheetName">The sheet the chunk is from, or null. A chunk never runs from one sheet into the next.</param>
/// <param name="StartRow">The row of its sheet the chunk starts at, or null.</param>
/// <param name="EndRow">The row of its sheet the chunk ends at, or null.</param>
/// <param name="StartLine">The line the chunk starts at, or null.</param>
/// <param name="EndLine">The line the chunk ends at, or null.</param>
/// <param name="AttachmentPosition">Which attachment of a mail the chunk is from, as mail_parts numbers it, or null for anything else.</param>
public sealed record EmbeddingStateChunk(
    string ChunkId,
    string ParentFileId,
    int? PageNumber,
    int? EndPageNumber,
    int ChunkIndex,
    string ChunkText,
    DateTimeOffset EmbeddedAtUtc,
    string? TextHash = null,
    int Generation = 0,
    string? SheetName = null,
    int? StartRow = null,
    int? EndRow = null,
    int? StartLine = null,
    int? EndLine = null,
    int? AttachmentPosition = null);
