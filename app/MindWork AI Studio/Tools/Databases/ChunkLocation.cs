namespace AIStudio.Tools.Databases;

/// <summary>
/// Where a stored chunk is in its document, as the index store keeps it.
/// </summary>
/// <remarks>
/// Each value is null when the document has no such thing, and for a chunk stored before the value
/// was kept: it reads as unknown then. The vector store keeps only the pages, so a hit of the vector
/// search learns the rest from the index store, cf. LocalIndexSearchService.
/// </remarks>
/// <param name="PageNumber">The page the chunk starts on.</param>
/// <param name="EndPageNumber">The page the chunk ends on.</param>
/// <param name="SheetName">The sheet of a spreadsheet the chunk is from.</param>
/// <param name="StartRow">The row of its sheet the chunk starts at.</param>
/// <param name="EndRow">The row of its sheet the chunk ends at.</param>
/// <param name="StartLine">The line the chunk starts at, for text, CSV and source code.</param>
/// <param name="EndLine">The line the chunk ends at.</param>
/// <param name="AttachmentPosition">Which attachment of a mail the chunk is from, as mail_parts numbers them, or null for the text of the mail itself and for any file.</param>
public sealed record ChunkLocation(
    int? PageNumber = null,
    int? EndPageNumber = null,
    string? SheetName = null,
    int? StartRow = null,
    int? EndRow = null,
    int? StartLine = null,
    int? EndLine = null,
    int? AttachmentPosition = null);