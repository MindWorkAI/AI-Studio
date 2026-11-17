using System.Text.RegularExpressions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Suggests the names of the other participants which a session shared with the research team of the BeyondFacts Lab
/// should not contain, from what the lab knows about the event.
/// </summary>
/// <remarks>
/// The lab knows names from three places: the field "Other participants", the headings of the research on the
/// participants, which names each person in a heading of its own, and the names in bold in the section about the
/// participants of a confirmed work state. A name is a run of two or three words with capital letters, or a single one
/// when it stands alone; longer runs give their last two words. The rules miss names and find words which are none,
/// so the person checks the suggestions and adds their own name.
/// </remarks>
public static partial class BflShareNames
{
    /// <summary>
    /// The key of the section of a work state about the other participants, in the steps "Event details" and
    /// "Strategy".
    /// </summary>
    private const string PARTICIPANTS_SECTION = "PARTICIPANTS";

    /// <summary>
    /// The most words a suggested name has.
    /// </summary>
    private const int MAX_NAME_WORDS = 3;

    /// <summary>
    /// Words which start a run of capitals without belonging to a name: articles, and roles which often stand before a
    /// name, e.g., "Moderatorin Anna Müller".
    /// </summary>
    private static readonly IReadOnlySet<string> LEADING_WORDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Der", "Die", "Das", "Dem", "Den", "Des", "Ein", "Eine", "Einer", "The", "A", "An",
        "Moderation", "Moderator", "Moderatorin", "Gast", "Gäste", "Referent", "Referentin", "Podium", "Panel", "Keynote",
        "Speaker", "Host", "Chair", "Moderated", "Mit", "With",
    };

    /// <summary>
    /// Suggests the names of the other participants.
    /// </summary>
    /// <param name="forms">The forms of the preparation, by their steps.</param>
    /// <param name="workStates">The work states of the preparation, by their steps.</param>
    /// <param name="participantsResearch">The research on the participants, or null when there is none.</param>
    /// <returns>The names, each once, in the order the lab found them.</returns>
    public static IReadOnlyList<string> Suggest(IReadOnlyDictionary<BflStep, BflStepFormData> forms, IReadOnlyDictionary<BflStep, BflWorkState> workStates, BflResearch? participantsResearch)
    {
        var candidates = new List<string>();
        foreach (var field in BflFieldCatalog.FIELDS.Where(field => field.NameRole is BflNameRole.PARTICIPANTS))
        {
            if (forms.TryGetValue(field.Step, out var form))
                candidates.AddRange(ListSeparatorPattern().Split(form.Get(field.Id)).SelectMany(NamesIn));
        }

        if (participantsResearch is not null)
            candidates.AddRange(HeadingPattern().Matches(participantsResearch.Text).SelectMany(heading => NamesIn(heading.Groups["text"].Value)));

        foreach (var step in BflStep.All)
        {
            if (workStates.GetValueOrDefault(step)?.Confirmed?.Sections.GetValueOrDefault(PARTICIPANTS_SECTION) is { } section)
                candidates.AddRange(BoldPattern().Matches(section).SelectMany(bold => NamesIn(bold.Groups["text"].Value)));
        }

        return Distinct(candidates);
    }

    /// <summary>
    /// Gets the names in a short text, e.g., an entry of a list or a heading.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The names found.</returns>
    internal static IEnumerable<string> NamesIn(string text)
    {
        var cleaned = MarkdownLinkPattern().Replace(text, "${label}");
        cleaned = MarkupPattern().Replace(cleaned, " ");

        // "Moderation: Anna Müller" and "Anna Müller – TUM" put the name and its role side by side:
        foreach (var segment in SegmentSeparatorPattern().Split(cleaned))
        {
            var words = segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(word => word.Trim(',', ';', '!', '?', '"', '„', '“', '”', '\'')).Where(word => word.Length > 0).ToList();
            if (words.Count is 1)
            {
                if (IsNameWord(words[0]) && !LEADING_WORDS.Contains(words[0]))
                    yield return words[0];

                continue;
            }

            var run = new List<string>();
            foreach (var word in words.Append(string.Empty))
            {
                // A particle continues a name, e.g., "Carla von der Heide", but never begins one:
                if (IsNameWord(word) || run.Count > 0 && BflAnonymizer.NAME_PARTICLES.Contains(word))
                {
                    run.Add(word);
                    continue;
                }

                if (NameOf(run) is { } name)
                    yield return name;

                run.Clear();
            }
        }
    }

    /// <summary>
    /// Gets the name a run of words with capital letters stands for, if any.
    /// </summary>
    /// <param name="run">The run, which may end with particles.</param>
    /// <returns>The name, or null when the run holds none.</returns>
    private static string? NameOf(List<string> run)
    {
        var words = run.SkipWhile(LEADING_WORDS.Contains).ToList();
        while (words.Count > 0 && BflAnonymizer.NAME_PARTICLES.Contains(words[^1]))
            words.RemoveAt(words.Count - 1);

        if (words.Count < 2)
            return null;

        // Particles do not count, so that "Carla von der Heide" stays whole:
        var significant = words.Count(word => !BflAnonymizer.NAME_PARTICLES.Contains(word));
        return string.Join(' ', significant > MAX_NAME_WORDS ? words.TakeLast(2) : words);
    }

    /// <summary>
    /// Tells whether a word may be part of a name: it begins with a capital letter, has at least two letters or is an
    /// initial, holds nothing but letters, hyphens, and apostrophes, and is no title.
    /// </summary>
    /// <param name="word">The word.</param>
    private static bool IsNameWord(string word) => NameWordPattern().IsMatch(word) && !BflAnonymizer.IsTitle(word);

    /// <summary>
    /// Removes names found twice, and a single word which is a part of a longer name found as well.
    /// </summary>
    /// <param name="candidates">The names, in the order found.</param>
    private static IReadOnlyList<string> Distinct(IReadOnlyList<string> candidates)
    {
        var parts = candidates.Where(candidate => candidate.Contains(' ')).SelectMany(candidate => candidate.Split(' ')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. candidates.Where(candidate => candidate.Contains(' ') || !parts.Contains(candidate)).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// What separates the entries of a list of participants: lines, semicolons, commas, slashes, and "and".
    /// </summary>
    [GeneratedRegex(@"\r?\n|;|,|/|\s+(?:und|and|sowie|&)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListSeparatorPattern();

    /// <summary>
    /// What separates a name from its role within an entry: a colon, a dash between spaces, or a remark in parentheses.
    /// </summary>
    [GeneratedRegex(@":|\s[-–—]\s|\([^)]*\)|\(|\)", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentSeparatorPattern();

    /// <summary>
    /// A heading of the third level, under which the research on the participants describes one person.
    /// </summary>
    [GeneratedRegex(@"^[ \t]{0,3}###[ \t]+(?<text>[^\n]+?)[ \t#]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    /// <summary>
    /// A text in bold.
    /// </summary>
    [GeneratedRegex(@"\*\*(?<text>[^*\n]+)\*\*|__(?<text>[^_\n]+)__", RegexOptions.CultureInvariant)]
    private static partial Regex BoldPattern();

    /// <summary>
    /// A link in Markdown, whose text stays.
    /// </summary>
    [GeneratedRegex(@"\[(?<label>[^\]\n]*)\]\([^)\n]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkPattern();

    /// <summary>
    /// The marks of Markdown which may surround a name: emphasis, code, and headings.
    /// </summary>
    [GeneratedRegex(@"\*+|_{2,}|`+|^#+", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex MarkupPattern();

    /// <summary>
    /// A word which may be part of a name.
    /// </summary>
    [GeneratedRegex(@"^(?:\p{Lu}[\p{L}\p{M}'’-]*\p{L}|\p{Lu}\.)$", RegexOptions.CultureInvariant)]
    private static partial Regex NameWordPattern();
}