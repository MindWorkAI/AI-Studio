namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One chunk as the chunking produced it, together with the page it starts on.
/// </summary>
/// <remarks>
/// The page is carried rather than read back out of the chunk text. The runtime states it, and
/// the chunking knows which source segment a chunk begins in, so nothing has to be derived from
/// a marker in the text — which is what used to leave Word files and continued passages without
/// a page.
/// </remarks>
/// <param name="Text">The chunk itself, overlap prefix included.</param>
/// <param name="PageNumber">The page the chunk's own content starts on, or null when it has none.</param>
internal sealed record EmbeddingChunk(string Text, int? PageNumber);