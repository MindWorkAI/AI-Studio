using AIStudio.Tools.Databases.IndexStore;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One document of a data source, ready to be embedded.
/// </summary>
/// <remarks>
/// Everything the shared part of a run needs to know about a document, whatever kind of data source
/// it comes from. Where the text comes from, and how it is cut into chunks, stays with the kind of
/// data source: the chunks are only read once the old vectors of the document are gone.
/// </remarks>
/// <param name="Key">What the vectors and the index row of the document are filed under. For a file, its full path.</param>
/// <param name="State">The index row of the document, written before its chunks with a chunk count of zero.</param>
/// <param name="DisplayName">How the document is called in messages for the user. For a file, its name.</param>
/// <param name="StreamChunks">Reads the document and yields its chunks.</param>
internal sealed record EmbeddingDocument(string Key, EmbeddingStateFile State, string DisplayName, Func<CancellationToken, IAsyncEnumerable<EmbeddingChunk>> StreamChunks);