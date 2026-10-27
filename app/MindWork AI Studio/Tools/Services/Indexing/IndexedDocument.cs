namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// What indexing a document stored: a new generation of its chunks, next to whatever the document had before.
/// </summary>
/// <remarks>
/// The new generation replaces the stored one only once the kind of data source records the
/// document as indexed, cf. DocumentRunProgress.RecordDocumentIndexedAsync. Until then, and for
/// good when it fails, the document keeps the version it had.
/// </remarks>
/// <param name="ChunkCount">How many chunks were stored.</param>
/// <param name="Generation">The generation of the chunks, cf. IndexedDocumentIds.CreateChunkIdSource.</param>
/// <param name="ChunkIds">The ids of the chunks, which are also the ids of their points.</param>
internal sealed record IndexedDocument(int ChunkCount, int Generation, IReadOnlyList<string> ChunkIds);