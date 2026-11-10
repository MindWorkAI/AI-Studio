namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Finds where the formatting of words changes, for formats which give each change a pair of tags, e.g.,
/// a word in bold in the middle of a sentence.
/// </summary>
/// <remarks>
/// The words in the most common formatting stay words, and those in any other formatting get a pair of their
/// own. Spaces never get one, since nobody sees how a space is formatted.
/// </remarks>
public static class FormatSpans
{
    /// <summary>
    /// How many changes of formatting one segment may carry as pairs of tags.
    /// </summary>
    /// <remarks>
    /// Every pair is one more thing a model has to keep in place. A segment whose formatting changes more often,
    /// e.g., a list of terms in bold, keeps one formatting for its new words instead and counts as simplified.
    /// </remarks>
    public const int MAX_PAIRS = 12;

    /// <summary>
    /// Finds the formatting most words have, by their letters.
    /// </summary>
    /// <param name="words">The words with the keys of their formatting.</param>
    /// <returns>The key of the most common formatting; the first one on a tie; empty for no words.</returns>
    public static string BaseKeyOf(IEnumerable<(string Text, string FormatKey)> words) => words
        .GroupBy(word => word.FormatKey)
        .OrderByDescending(group => group.Sum(word => word.Text.Count(char.IsLetter)))
        .Select(group => group.Key)
        .FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// Joins words which follow each other into spans of the same formatting.
    /// </summary>
    /// <remarks>
    /// Spaces in a formatting of their own join their neighbor, since a pair around them would only burden the
    /// model. Spaces in the most common formatting stay apart, e.g., between a word in bold and one in italics.
    /// </remarks>
    /// <param name="words">The words in their order, with their formatting and its key.</param>
    /// <param name="baseFormatKey">The key of the most common formatting, see BaseKeyOf.</param>
    public static List<FormatSpan<TFormat>> Of<TFormat>(IEnumerable<(string Text, TFormat Format, string FormatKey)> words, string baseFormatKey)
    {
        var spans = new List<FormatSpan<TFormat>>();
        foreach (var (text, format, formatKey) in words)
        {
            var isBlankOfItsOwn = string.IsNullOrWhiteSpace(text) && formatKey != baseFormatKey;
            if (spans.Count > 0 && (spans[^1].FormatKey == formatKey || isBlankOfItsOwn))
                spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
            else if (spans.Count > 0 && string.IsNullOrWhiteSpace(spans[^1].Text) && spans[^1].FormatKey != baseFormatKey)
                spans[^1] = new FormatSpan<TFormat>(formatKey, format, spans[^1].Text + text);
            else
                spans.Add(new FormatSpan<TFormat>(formatKey, format, text));
        }

        return spans;
    }
}