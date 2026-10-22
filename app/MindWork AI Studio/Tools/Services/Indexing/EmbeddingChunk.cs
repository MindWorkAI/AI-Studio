namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One chunk as the chunking produced it, together with the pages it spans.
/// </summary>
/// <remarks>
/// The pages are carried rather than read back out of the chunk text. The runtime states them, and
/// the chunking knows which source segments a chunk covers, so nothing has to be derived from a
/// marker in the text — which is what used to leave Word files and continued passages without a
/// page.
/// </remarks>
/// <param name="Text">The chunk itself, overlap prefix included.</param>
/// <param name="PageNumber">The page the chunk's own content starts on, or null when it has none.</param>
/// <param name="EndPageNumber">The page the chunk ends on, or null when it has none. Equal to the start page when the chunk stays on one.</param>
internal sealed record EmbeddingChunk(string Text, int? PageNumber, int? EndPageNumber);