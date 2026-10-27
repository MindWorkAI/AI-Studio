namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One piece of a text as its source delivered it: a page, a slide, a sheet or a section.
/// </summary>
/// <param name="Text">The text of the piece.</param>
/// <param name="TokenCount">Its token count for the embedding provider, when the source already knows it.</param>
/// <param name="Location">Where it is: its page, its sheet and row, or its line; nothing when the source does not say.</param>
internal sealed record TextSegment(string Text, int? TokenCount, SourceLocation Location);