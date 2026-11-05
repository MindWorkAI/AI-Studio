using System.Text.RegularExpressions;

namespace AIStudio.Tools;

/// <summary>
/// The words of a search, and where a text holds them: to find chats, and to show the user what was found.
/// </summary>
/// <remarks>
/// Every word counts on its own and ignores case, so "alpha budget" finds "Budget of Project Alpha".
/// Words in quotes count as one exact phrase instead, so "\"test case\"" finds only "test case".
/// </remarks>
public static partial class SearchText
{
    /// <summary>
    /// The quotes which enclose a phrase: the plain one, and the typographic ones which keyboards and
    /// systems put in its place, such as the German low and high ones.
    /// </summary>
    private static readonly char[] QUOTES = ['"', '“', '”', '„'];

    /// <summary>
    /// The words of a search, each counted once: separated by spaces, or enclosed in quotes as one
    /// phrase.
    /// </summary>
    /// <remarks>
    /// A quote without its closing one counts for nothing, so its words count on their own: who did not
    /// mean to search a phrase still finds what they typed. Spaces within a phrase count as one.
    /// </remarks>
    public static IReadOnlyList<string> ParseTerms(string? searchText)
    {
        var text = searchText ?? string.Empty;
        var terms = new List<string>();
        var position = 0;
        while (position < text.Length)
        {
            var phraseStart = text.IndexOfAny(QUOTES, position);
            var phraseEnd = phraseStart < 0 ? -1 : text.IndexOfAny(QUOTES, phraseStart + 1);
            if (phraseEnd < 0)
            {
                AddWords(text[position..]);
                break;
            }

            AddWords(text[position..phraseStart]);
            var phrase = string.Join(' ', SplitWords(text[(phraseStart + 1)..phraseEnd]));
            if (phrase.Length > 0)
                terms.Add(phrase);

            position = phraseEnd + 1;
        }

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        void AddWords(string part) => terms.AddRange(SplitWords(part));

        static IEnumerable<string> SplitWords(string part) => part
            .Split([' ', ..QUOTES], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// The words which the given text lacks.
    /// </summary>
    public static IReadOnlyList<string> MissingTerms(string text, IReadOnlyList<string> terms) => terms
        .Where(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
        .ToList();

    /// <summary>
    /// Splits a text into the words found in it and the text in between, so that the words can be marked.
    /// </summary>
    /// <remarks>
    /// Every occurrence of every word is marked. Occurrences which overlap or touch each other form one
    /// part. A text without any of the words is a single part, which is not marked.
    /// </remarks>
    public static IReadOnlyList<SearchTextPart> Highlight(string text, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrEmpty(text) || terms.Count == 0)
            return [new(text, IsMatch: false)];

        var isMarked = new bool[text.Length];
        foreach (var term in terms)
        {
            if (term.Length == 0)
                continue;

            var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                Array.Fill(isMarked, true, index, term.Length);
                index = text.IndexOf(term, index + term.Length, StringComparison.OrdinalIgnoreCase);
            }
        }

        var parts = new List<SearchTextPart>();
        var start = 0;
        for (var position = 1; position <= text.Length; position++)
        {
            if (position < text.Length && isMarked[position] == isMarked[start])
                continue;

            parts.Add(new(text[start..position], isMarked[start]));
            start = position;
        }

        return parts;
    }

    /// <summary>
    /// The passage of a message around the first of the given words, as plain text on a single line.
    /// </summary>
    /// <remarks>
    /// The passage starts a few words before the word, so that the word shows even where the line gets
    /// cut at its end, as in a narrow sidebar. A word which lies in a part of the message that does not
    /// show, such as the address of a link, leaves the passage at the beginning of the message.
    /// </remarks>
    /// <param name="markdown">The text of the message, in Markdown.</param>
    /// <param name="terms">The words to look for.</param>
    /// <param name="charactersBefore">How much of the text before the word the passage keeps at most.</param>
    /// <param name="maximumLength">How long the passage gets at most, apart from the ellipses.</param>
    public static string FindPassage(string markdown, IReadOnlyList<string> terms, int charactersBefore = 30, int maximumLength = 160)
    {
        var text = ToPlainLine(markdown);
        var firstMatch = terms
            .Select(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(0)
            .Min();

        //
        // The passage starts with a whole word, unless the text before the match is a single long one:
        //
        var start = Math.Max(0, firstMatch - charactersBefore);
        if (start > 0)
        {
            var nextSpace = text.IndexOf(' ', start);
            if (nextSpace >= 0 && nextSpace < firstMatch)
                start = nextSpace + 1;
        }

        var end = Math.Min(text.Length, start + maximumLength);
        var leading = start > 0 ? "… " : string.Empty;
        var trailing = end < text.Length ? " …" : string.Empty;
        return $"{leading}{text[start..end]}{trailing}";
    }

    /// <summary>
    /// Markdown as a single line of plain text, as the user reads it: links and images keep their text,
    /// while the marks of emphasis, code, headings, quotes, and lists go away, and so do line breaks.
    /// </summary>
    private static string ToPlainLine(string markdown)
    {
        var text = LinkOrImage().Replace(markdown, "$1");
        text = LineMarker().Replace(text, string.Empty);
        text = InlineMarker().Replace(text, string.Empty);
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"!?\[([^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkOrImage();

    [GeneratedRegex(@"^[ \t]{0,3}(?:#{1,6}|>|[-*+]|\d+\.)[ \t]+", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex LineMarker();

    [GeneratedRegex(@"\*\*|__|~~|`+|\*", RegexOptions.CultureInvariant)]
    private static partial Regex InlineMarker();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}

/// <summary>
/// A part of a text: either one of the words of a search, or the text in between.
/// </summary>
/// <param name="Text">The text of the part.</param>
/// <param name="IsMatch">Whether the part is one of the words, which the user sees marked.</param>
public readonly record struct SearchTextPart(string Text, bool IsMatch);