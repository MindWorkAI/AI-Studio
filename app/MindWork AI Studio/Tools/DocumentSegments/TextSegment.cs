namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// A segment of a document which is text itself: where its words lie in the source, and which markup each of
/// its tags stands for.
/// </summary>
/// <param name="Start">Where the segment starts in the source.</param>
/// <param name="End">Where the segment ends in the source, exclusive.</param>
/// <param name="Segment">The segment, as a model gets it.</param>
/// <param name="Openings">The markup which the opening tag of each pair stands for, e.g., "[" of a Markdown link.</param>
/// <param name="Closings">The markup which the closing tag of each pair stands for, e.g., "](https://example.org)".</param>
/// <param name="Placeholders">The markup which each tag of its own stands for, e.g., a code span.</param>
/// <param name="FormatWords">Makes the words of this segment fit the format in place of what the writer gets, e.g., within an optional argument of LaTeX, which a closing bracket would end; null for the writer's.</param>
public sealed record TextSegment(
    int Start,
    int End,
    DocumentSegment Segment,
    IReadOnlyDictionary<int, string> Openings,
    IReadOnlyDictionary<int, string> Closings,
    IReadOnlyDictionary<int, string> Placeholders,
    Func<string, string>? FormatWords = null);