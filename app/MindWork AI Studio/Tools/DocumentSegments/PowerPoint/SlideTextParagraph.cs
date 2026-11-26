using DocumentFormat.OpenXml;

using A = DocumentFormat.OpenXml.Drawing;

namespace AIStudio.Tools.DocumentSegments.PowerPoint;

/// <summary>
/// A paragraph of a slide or of its notes as a segment, with all the writer needs to put it together again.
/// </summary>
/// <param name="Paragraph">The paragraph.</param>
/// <param name="Segment">Its words with tags.</param>
/// <param name="FormatPairs">The formatting of the words within each pair, e.g., bold or a link.</param>
/// <param name="Placeholders">The element each tag of its own stands for, e.g., a line break or the field of the slide number.</param>
/// <param name="BaseFormat">The most common formatting of the words, which new words outside of any pair get.</param>
/// <param name="HasMixedFormatting">Whether the words differ in their formatting more often than pairs of formatting allow, so that the new words keep only the most common one.</param>
internal sealed record SlideTextParagraph(
    A.Paragraph Paragraph,
    DocumentSegment Segment,
    IReadOnlyDictionary<int, A.RunProperties?> FormatPairs,
    IReadOnlyDictionary<int, OpenXmlElement> Placeholders,
    A.RunProperties? BaseFormat,
    bool HasMixedFormatting);