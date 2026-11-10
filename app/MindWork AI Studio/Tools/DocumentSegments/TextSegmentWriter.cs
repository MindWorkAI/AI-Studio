using System.Text;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Writes the new texts of the segments into a document which is text itself, leaving everything around them
/// as it is.
/// </summary>
public static class TextSegmentWriter
{
    /// <summary>
    /// Replaces the segments of a source with their new texts.
    /// </summary>
    /// <remarks>
    /// A new text whose tags do not fit those of its segment still gets written: its pairs whose both tags
    /// arrived go back, the others go, and the tags of their own which the model dropped follow the words.
    /// A segment whose new text equals its source stays exactly as the source holds it.
    /// </remarks>
    /// <param name="source">The text of the document.</param>
    /// <param name="segments">The segments of the document, as the format reads them from this source, in the order of the document.</param>
    /// <param name="results">What became of the segments.</param>
    /// <param name="formatWords">Makes words fit the format, e.g., removes line breaks a model wrote into a Markdown paragraph; a segment may bring its own, see TextSegment.FormatWords.</param>
    /// <param name="text">The text of the new document, or the source when the results do not cover it.</param>
    /// <returns>How the segments were written, or SOURCE_CHANGED when the results miss one of them.</returns>
    public static SegmentWriteResult Apply(string source, IReadOnlyList<TextSegment> segments, SegmentResults results, Func<string, string> formatWords, out string text)
    {
        text = source;
        if (segments.Any(segment => !results.Covers(segment.Segment)))
            return SegmentWriteResult.SOURCE_CHANGED;

        var exactCount = 0;
        var simplifiedCount = 0;
        var builder = new StringBuilder(source.Length);
        var position = 0;
        foreach (var segment in segments)
        {
            builder.Append(source, position, segment.Start - position);
            position = segment.End;
            if (!results.TryGetText(segment.Segment, out var newText) || newText == segment.Segment.Key)
            {
                builder.Append(source, segment.Start, segment.End - segment.Start);
                continue;
            }

            if (Render(segment, newText, formatWords, builder))
                exactCount++;
            else
                simplifiedCount++;
        }

        builder.Append(source, position, source.Length - position);
        text = builder.ToString();
        return new(SegmentWriteOutcome.WRITTEN, exactCount, simplifiedCount);
    }

    /// <summary>
    /// Writes the new text of a segment with the markup of its tags.
    /// </summary>
    /// <returns>Whether the tags of the new text fit those of the segment.</returns>
    private static bool Render(TextSegment segment, string newText, Func<string, string> formatWords, StringBuilder builder)
    {
        formatWords = segment.FormatWords ?? formatWords;
        if (!TaggedText.TryRead(newText, out var taggedText, out _))
        {
            builder.Append(formatWords(TaggedText.RemoveTags(newText)));
            AppendMissingPlaceholders(segment, [], builder);
            return false;
        }

        var fits = taggedText.HasSameTagsAs(segment.Segment.Source, out _);
        var openIds = taggedText.Tokens.Where(token => token.Kind is TaggedTokenKind.OPEN).Select(token => token.Id).ToHashSet();
        var closeIds = taggedText.Tokens.Where(token => token.Kind is TaggedTokenKind.CLOSE).Select(token => token.Id).ToHashSet();
        var writtenPlaceholders = new HashSet<int>();
        foreach (var token in taggedText.Tokens)
        {
            switch (token.Kind)
            {
                case TaggedTokenKind.TEXT:
                    builder.Append(formatWords(token.Text));
                    break;

                case TaggedTokenKind.OPEN when closeIds.Contains(token.Id) && segment.Openings.TryGetValue(token.Id, out var opening):
                    builder.Append(opening);
                    break;

                case TaggedTokenKind.CLOSE when openIds.Contains(token.Id) && segment.Closings.TryGetValue(token.Id, out var closing):
                    builder.Append(closing);
                    break;

                case TaggedTokenKind.PLACEHOLDER when segment.Placeholders.TryGetValue(token.Id, out var placeholder) && writtenPlaceholders.Add(token.Id):
                    builder.Append(placeholder);
                    break;
            }
        }

        AppendMissingPlaceholders(segment, writtenPlaceholders, builder);
        return fits;
    }

    /// <summary>
    /// Writes the tags of their own which the model dropped after the words, so that nothing of the source gets lost.
    /// </summary>
    private static void AppendMissingPlaceholders(TextSegment segment, HashSet<int> writtenPlaceholders, StringBuilder builder)
    {
        foreach (var (id, placeholder) in segment.Placeholders.OrderBy(entry => entry.Key))
        {
            if (writtenPlaceholders.Contains(id))
                continue;

            if (builder.Length > 0 && !char.IsWhiteSpace(builder[^1]))
                builder.Append(' ');

            builder.Append(placeholder);
        }
    }
}