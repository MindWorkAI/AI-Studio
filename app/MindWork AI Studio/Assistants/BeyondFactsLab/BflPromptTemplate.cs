using System.Text.RegularExpressions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Fills the placeholders of a prompt of the BeyondFacts Lab, written as {{NAME}} in upper case.
/// </summary>
/// <remarks>
/// A prompt with a placeholder nobody fills is an error, not a prompt with a gap: the model would read the
/// placeholder as text. The values are inserted in one pass, so a value which contains a placeholder itself, such
/// as a detail the person entered, stays as it is. A placeholder which stands on a line of its own and gets an empty
/// value takes its line and the empty lines after it along, so that an optional section, such as the one about the
/// guidelines of an organization, leaves no gap.
/// </remarks>
public static partial class BflPromptTemplate
{
    /// <summary>
    /// The language the model answers in.
    /// </summary>
    public const string RESPONSE_LANGUAGE = "RESPONSE_LANGUAGE";

    /// <summary>
    /// How the model addresses the person.
    /// </summary>
    public const string ADDRESS_FORM = "ADDRESS_FORM";

    /// <summary>
    /// The guide of the lab.
    /// </summary>
    public const string KNOWLEDGE = "KNOWLEDGE";

    /// <summary>
    /// The prompt of the step the chat belongs to.
    /// </summary>
    public const string STEP = "STEP";

    /// <summary>
    /// The material the person added to the preparation.
    /// </summary>
    public const string MATERIALS = "MATERIALS";

    /// <summary>
    /// What the person entered into the forms.
    /// </summary>
    public const string DETAILS = "DETAILS";

    /// <summary>
    /// The key details the forms still lack.
    /// </summary>
    public const string MISSING_DETAILS = "MISSING_DETAILS";

    /// <summary>
    /// The work states the person confirmed.
    /// </summary>
    public const string WORK_STATES = "WORK_STATES";

    /// <summary>
    /// The sections of the work state the model writes.
    /// </summary>
    public const string SECTIONS = "SECTIONS";

    /// <summary>
    /// The empty fields of the form for which the model may suggest values.
    /// </summary>
    public const string FIELDS = "FIELDS";

    /// <summary>
    /// The steps without a confirmed work state, whose topics a result must not fill with guesses.
    /// </summary>
    public const string MISSING_WORK_STATES = "MISSING_WORK_STATES";

    /// <summary>
    /// What a chat can do on the web with the current setup, and what keeps it from more, see BflToolStatus.
    /// </summary>
    public const string TOOLS = "TOOLS";

    /// <summary>
    /// What the research agents found about the event, for the step "Strategy".
    /// </summary>
    public const string RESEARCH = "RESEARCH";

    /// <summary>
    /// How the model treats the guidelines of the organization, only while there are any, see BflOrganizationRules.
    /// </summary>
    public const string ORGANIZATION_RULES = "ORGANIZATION_RULES";

    /// <summary>
    /// What a research agent looks into, see BflResearchTopic.
    /// </summary>
    public const string TOPIC = "TOPIC";

    /// <summary>
    /// How many calls of tools a research agent has at most.
    /// </summary>
    public const string TOOL_CALLS = "TOOL_CALLS";

    /// <summary>
    /// The public details about the event, the only ones a research agent receives.
    /// </summary>
    public const string PUBLIC_DETAILS = "PUBLIC_DETAILS";

    /// <summary>
    /// The person an agent of the research on people looks into: the name and the entry in the list of participants.
    /// </summary>
    public const string PERSON = "PERSON";

    /// <summary>
    /// What the earlier stages of the research on a person found.
    /// </summary>
    public const string EARLIER_STAGES = "EARLIER_STAGES";

    /// <summary>
    /// The list of participants from which the lab suggests whom to research.
    /// </summary>
    public const string PARTICIPANTS = "PARTICIPANTS";

    /// <summary>
    /// How many people the lab suggests to research at most.
    /// </summary>
    public const string SELECTION_SIZE = "SELECTION_SIZE";

    /// <summary>
    /// Fills every placeholder of a template.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="values">The value of each placeholder, by its name.</param>
    /// <returns>The prompt.</returns>
    /// <exception cref="FormatException">The template has a placeholder without a value.</exception>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        // Only the template changes here, before any value goes in, so that values still go in in one pass:
        var withoutEmptyLines = StandAlonePlaceholderPattern().Replace(template, match => values.TryGetValue(match.Groups["name"].Value, out var value) && value.Length is 0 ? string.Empty : match.Value);
        return PlaceholderPattern().Replace(withoutEmptyLines, match =>
        {
            var name = match.Groups["name"].Value;
            return values.TryGetValue(name, out var value) ? value : throw new FormatException($"The prompt has the placeholder '{name}', which has no value.");
        });
    }

    /// <summary>
    /// Finds the placeholders of a template, as Fill reads them.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The name of each placeholder, once each, in the order they first appear.</returns>
    public static IReadOnlyList<string> NamesIn(string template) => [.. PlaceholderPattern().Matches(template).Select(match => match.Groups["name"].Value).Distinct(StringComparer.Ordinal)];

    [GeneratedRegex(@"\{\{\s*(?<name>[^{}]*?)\s*\}\}")]
    private static partial Regex PlaceholderPattern();

    /// <summary>
    /// Finds a placeholder which stands on a line of its own, together with the line break and the empty lines after it.
    /// </summary>
    [GeneratedRegex(@"^[ \t]*\{\{\s*(?<name>[^{}]*?)\s*\}\}[ \t]*(?:\r?\n[ \t]*)*(?:\r?\n|\z)", RegexOptions.Multiline)]
    private static partial Regex StandAlonePlaceholderPattern();
}