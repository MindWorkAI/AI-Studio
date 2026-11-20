using System.Text;
using System.Text.RegularExpressions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A person whom the research on the participants of the BeyondFacts Lab lists as taking part in the event.
/// </summary>
/// <remarks>
/// The agent of the topic "Participants" gives every person a heading of the third level with nothing but the name, see
/// Prompts/research/participants.md. The lab reads these headings once, when it stores the research, and keeps the list
/// as it is from then on, see BflResearch.Participants.
/// </remarks>
public sealed partial class BflParticipant
{
    /// <summary>
    /// How many characters a name has at most. A longer heading is no name.
    /// </summary>
    public const int MAX_NAME_CHARACTERS = 200;

    /// <summary>
    /// How many people a list holds at most. The prompt asks for 40 at most; the rest is left out.
    /// </summary>
    public const int MAX_PARTICIPANTS = 50;

    /// <summary>
    /// Gets or sets the name of the person, as the heading gives it.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what the list says about the person below the heading, in Markdown: the role at the event, the
    /// institution or function, and the source.
    /// </summary>
    public string Entry { get; set; } = string.Empty;

    /// <summary>
    /// Tells whether this is the person with a name, regardless of case and of the spaces between the words.
    /// </summary>
    /// <param name="name">The name.</param>
    public bool HasName(string name) => string.Equals(NameKey(this.Name), NameKey(name), StringComparison.Ordinal);

    /// <summary>
    /// Reads the people from the answer of the agent of the topic "Participants".
    /// </summary>
    /// <remarks>
    /// Every heading of the third level starts a person; a heading of a higher level ends it, one of a lower level
    /// belongs to its entry. Markdown in a heading, such as emphasis or a link, is taken off. A heading which is empty
    /// or too long is no person, and a name which comes twice counts once, with its first entry.
    /// </remarks>
    /// <param name="text">The answer, with its lines ending in LF.</param>
    /// <returns>The people, in the order of the answer.</returns>
    public static IReadOnlyList<BflParticipant> ListIn(string text)
    {
        var participants = new List<BflParticipant>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        BflParticipant? current = null;
        var entry = new StringBuilder();

        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var heading = HeadingPattern().Match(line);
            if (!heading.Success || heading.Groups["level"].Length > 3)
            {
                if (current is not null)
                    entry.Append(line).Append('\n');

                continue;
            }

            Finish();
            if (heading.Groups["level"].Length < 3 || participants.Count >= MAX_PARTICIPANTS)
                continue;

            var name = CleanName(heading.Groups["text"].Value);
            if (name.Length is 0 or > MAX_NAME_CHARACTERS || !keys.Add(NameKey(name)))
                continue;

            current = new BflParticipant { Name = name };
            participants.Add(current);
        }

        Finish();
        return participants;

        void Finish()
        {
            if (current is not null)
                current.Entry = entry.ToString().Trim();

            current = null;
            entry.Clear();
        }
    }

    /// <summary>
    /// Gets what decides whether two names are the same: the words in capitals, separated by one space.
    /// </summary>
    /// <param name="name">The name.</param>
    public static string NameKey(string name) => string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    /// <summary>
    /// Takes the Markdown off a heading and joins its words with single spaces.
    /// </summary>
    /// <param name="heading">The text of the heading.</param>
    private static string CleanName(string heading)
    {
        var cleaned = MarkdownLinkPattern().Replace(heading, "${label}");
        cleaned = MarkupPattern().Replace(cleaned, string.Empty);
        return string.Join(' ', cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// A heading in Markdown, with its level and its text.
    /// </summary>
    [GeneratedRegex(@"^[ \t]{0,3}(?<level>#{1,6})[ \t]+(?<text>.*?)[ \t#]*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    /// <summary>
    /// A link in Markdown, whose text stays.
    /// </summary>
    [GeneratedRegex(@"\[(?<label>[^\]\n]*)\]\([^)\n]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkPattern();

    /// <summary>
    /// The marks of Markdown which may surround a name: emphasis and code.
    /// </summary>
    [GeneratedRegex(@"\*+|_{2,}|`+", RegexOptions.CultureInvariant)]
    private static partial Regex MarkupPattern();
}