using System.Text.RegularExpressions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Fills the placeholders of a prompt of the BeyondFacts Lab, written as {{NAME}} in upper case.
/// </summary>
/// <remarks>
/// A prompt with a placeholder nobody fills is an error, not a prompt with a gap: the model would read the
/// placeholder as text. The values are inserted in one pass, so a value which contains a placeholder itself, such
/// as a detail the person entered, stays as it is.
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
    /// Fills every placeholder of a template.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="values">The value of each placeholder, by its name.</param>
    /// <returns>The prompt.</returns>
    /// <exception cref="FormatException">The template has a placeholder without a value.</exception>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values) => PlaceholderPattern().Replace(template, match =>
    {
        var name = match.Groups["name"].Value;
        return values.TryGetValue(name, out var value) ? value : throw new FormatException($"The prompt has the placeholder '{name}', which has no value.");
    });

    /// <summary>
    /// Finds the placeholders of a template, as Fill reads them.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The name of each placeholder, once each, in the order they first appear.</returns>
    public static IReadOnlyList<string> NamesIn(string template) => [.. PlaceholderPattern().Matches(template).Select(match => match.Groups["name"].Value).Distinct(StringComparer.Ordinal)];

    [GeneratedRegex(@"\{\{\s*(?<name>[^{}]*?)\s*\}\}")]
    private static partial Regex PlaceholderPattern();
}