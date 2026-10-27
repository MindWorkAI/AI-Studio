namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// A chunk the index store holds, as far as indexing its document again needs to know it.
/// </summary>
/// <param name="ChunkId">The id of the chunk, which is also the id of its point in the vector store.</param>
/// <param name="TextHash">The hash of its text, cf. ChunkTextHash.</param>
/// <param name="Generation">How many times its document was indexed before it, cf. IndexedDocumentIds.CreateChunkIdSource.</param>
public sealed record StoredChunk(string ChunkId, string TextHash, int Generation);