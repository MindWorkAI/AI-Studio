namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// A whole text, together with the pieces its source delivered it in.
/// </summary>
/// <param name="Text">The pieces joined into one text.</param>
/// <param name="SourceSegments">The pieces, in order.</param>
internal sealed record SegmentedText(string Text, IReadOnlyList<TextSegment> SourceSegments);