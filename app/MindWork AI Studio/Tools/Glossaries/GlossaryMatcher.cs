using System.Globalization;
using System.Text;

namespace AIStudio.Tools.Glossaries;

public static class GlossaryMatcher
{
    /// <summary>
    /// Terms shorter than this would match almost every text, e.g. a single letter.
    /// </summary>
    private const int MIN_TERM_LENGTH = 2;

    private static readonly CompareInfo COMPARE_INFO = CultureInfo.InvariantCulture.CompareInfo;

    /// <summary>
    /// Finds the entries with at least one term, in any of its languages, that occurs in the source text.
    /// </summary>
    /// <remarks>
    /// A term counts as soon as it is part of the text, ignoring case and line breaks. This is on purpose:
    /// German compounds such as "Arbeitszeitregelung" contain the term "Arbeitszeit", and a match on whole
    /// words alone would miss them. A needless entry costs a few tokens, a missing one the exact translation
    /// the user asked for.
    /// </remarks>
    /// <param name="table">The glossary table to search.</param>
    /// <param name="sourceText">The text that is going to be translated.</param>
    /// <returns>The table with the matching entries only; it has no entries when nothing matched.</returns>
    public static GlossaryTable FindEntries(GlossaryTable table, string sourceText)
    {
        var normalizedSource = NormalizeWhitespace(sourceText);
        if (normalizedSource.Length == 0)
            return table with { Entries = [] };

        var entries = table.Entries
            .Where(entry => entry.Any(term => Occurs(NormalizeWhitespace(term), normalizedSource)))
            .ToList();

        return table with { Entries = entries };
    }

    private static bool Occurs(string term, string source) => term.Length >= MIN_TERM_LENGTH && COMPARE_INFO.IndexOf(source, term, CompareOptions.IgnoreCase) >= 0;

    //
    // A term may break across lines in the source text, or carry a double space in the glossary;
    // both must not keep it from matching:
    //
    private static string NormalizeWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}