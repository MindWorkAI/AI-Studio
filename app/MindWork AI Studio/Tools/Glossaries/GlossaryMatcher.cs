using System.Runtime.CompilerServices;
using System.Text;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Finds the entries of a glossary whose term, or one of its aliases, occurs in a text.
/// </summary>
/// <remarks>
/// A term counts as soon as it is part of the text, ignoring case and line breaks. This is on purpose:
/// German compounds such as "Arbeitszeitregelung" contain the term "Arbeitszeit", and a match on whole
/// words alone would miss them. A needless entry costs a few tokens, a missing one the exact translation
/// the user asked for.<br/><br/>
///
/// The terms are normalized once per list of entries. The GlossaryStore hands out the same list until
/// the glossary is saved again, so a matcher lives exactly as long as the entries it was made for.
/// The search is ordinal, which .NET vectorizes; it handles thousands of terms in a long text quickly
/// enough that no multi-pattern algorithm such as Aho-Corasick is needed.
/// </remarks>
public sealed class GlossaryMatcher
{
    /// <summary>
    /// Terms shorter than this would match almost every text, e.g. a single letter.
    /// </summary>
    private const int MIN_TERM_LENGTH = 2;

    private static readonly ConditionalWeakTable<IReadOnlyList<GlossaryEntry>, GlossaryMatcher> MATCHERS = new();

    private readonly IReadOnlyList<GlossaryEntry> entries;

    private readonly IReadOnlyList<string[]> normalizedTermsPerEntry;

    private GlossaryMatcher(IReadOnlyList<GlossaryEntry> entries)
    {
        this.entries = entries;
        this.normalizedTermsPerEntry = entries
            .Select(entry => new[] { entry.Term }
                .Concat(entry.Aliases ?? [])
                .Select(Normalize)
                .Where(term => term.Length >= MIN_TERM_LENGTH)
                .Distinct(StringComparer.Ordinal)
                .ToArray())
            .ToList();
    }

    /// <summary>
    /// Gets the matcher for a list of entries, reusing the one made before for the same list.
    /// </summary>
    /// <param name="entries">The entries of a glossary, as the GlossaryStore hands them out.</param>
    /// <returns>The matcher for these entries.</returns>
    public static GlossaryMatcher For(IReadOnlyList<GlossaryEntry> entries) => MATCHERS.GetValue(entries, list => new GlossaryMatcher(list));

    /// <summary>
    /// Finds the entries which occur in a text.
    /// </summary>
    /// <param name="text">The text to search, e.g. the text which is going to be translated.</param>
    /// <returns>The matching entries, in the order of the glossary; none when nothing matched.</returns>
    public IReadOnlyList<GlossaryEntry> FindEntries(string text)
    {
        var normalizedText = Normalize(text);
        if (normalizedText.Length == 0)
            return [];

        var matches = new List<GlossaryEntry>();
        for (var index = 0; index < this.entries.Count; index++)
            if (this.normalizedTermsPerEntry[index].Any(term => normalizedText.Contains(term, StringComparison.Ordinal)))
                matches.Add(this.entries[index]);

        return matches;
    }

    //
    // A term may break across lines in the text, or carry a double space in the glossary; neither must
    // keep it from matching. Lowercasing both sides once is much faster than comparing every term while
    // ignoring the case:
    //
    private static string Normalize(string text)
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

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}