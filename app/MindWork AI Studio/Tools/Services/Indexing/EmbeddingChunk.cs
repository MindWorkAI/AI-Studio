namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One chunk as the chunking produced it, together with where it starts and where it ends.
/// </summary>
/// <remarks>
/// The locations are carried rather than read back out of the chunk text. The runtime states them,
/// and the chunking knows which source segments a chunk covers, so nothing has to be derived from a
/// marker in the text — which is what used to leave Word files and continued passages without a
/// page.
/// </remarks>
/// <param name="Text">The chunk itself, overlap prefix included.</param>
/// <param name="Start">Where the chunk's own content starts: its page, its sheet and row, or its line.</param>
/// <param name="End">Where the chunk ends. Equal to the start when the chunk stays within one page, row or line.</param>
/// <param name="AttachmentPosition">Which attachment of a mail the chunk is from, counted from zero as the index numbers them, or null for the text of the mail itself and for anything but a mail.</param>
internal sealed record EmbeddingChunk(string Text, SourceLocation Start, SourceLocation End, int? AttachmentPosition = null)
{
    /// <summary>
    /// The page the chunk's own content starts on, or null when it has none.
    /// </summary>
    public int? PageNumber => this.Start.PageNumber;

    /// <summary>
    /// The page the chunk ends on, or null when it has none.
    /// </summary>
    public int? EndPageNumber => this.End.PageNumber;
}