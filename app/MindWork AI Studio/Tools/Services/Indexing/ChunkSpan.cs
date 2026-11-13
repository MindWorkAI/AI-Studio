namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Where a chunk starts and where it ends in its document, cf. TextChunker.Locate.
/// </summary>
/// <param name="Start">Where the chunk starts.</param>
/// <param name="End">Where the chunk ends.</param>
internal readonly record struct ChunkSpan(SourceLocation Start, SourceLocation End);