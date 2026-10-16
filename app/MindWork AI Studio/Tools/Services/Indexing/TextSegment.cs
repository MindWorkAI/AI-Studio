namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One piece of a text as its source delivered it: a page, a slide, a sheet or a section.
/// </summary>
/// <param name="Text">The text of the piece.</param>
/// <param name="TokenCount">Its token count for the embedding provider, when the source already knows it.</param>
/// <param name="PageNumber">The page it is on, or null when the source has no pages.</param>
internal sealed record TextSegment(string Text, int? TokenCount, int? PageNumber);