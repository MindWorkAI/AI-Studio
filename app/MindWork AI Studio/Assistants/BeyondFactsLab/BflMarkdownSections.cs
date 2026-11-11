using System.Text.RegularExpressions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Reads the work state a model wrote as Markdown, with a heading "## [KEY] Title" for each section.
/// </summary>
/// <remarks>
/// Markdown needs no escaping of line breaks and quotation marks, so even weaker models write it reliably, unlike
/// JSON. The keys decide, not the titles, which the model may translate. A heading "## [FIELD:id]" holds a value the
/// model suggests for a field of the form. The reader is strict: it never guesses what a section or a suggestion
/// could mean. A wrong section becomes a problem, which the model may correct once; a suggestion which does not fit
/// its field is left out, since the person fills in the field just as well.
/// </remarks>
public static partial class BflMarkdownSections
{
    /// <summary>
    /// The beginning of the key of a section which suggests a value for a field of the form.
    /// </summary>
    public const string FIELD_PREFIX = "FIELD:";

    /// <summary>
    /// Splits an answer into its sections.
    /// </summary>
    /// <remarks>
    /// Text before the first heading, such as an introduction of the model, belongs to no section and is left out,
    /// as are the lines of a code block around the whole answer.
    /// </remarks>
    /// <param name="answer">The answer of the model.</param>
    /// <returns>The sections, in the order of the answer.</returns>
    public static IReadOnlyList<BflMarkdownSection> Split(string answer)
    {
        var sections = new List<BflMarkdownSection>();
        string? key = null;
        var lines = new List<string>();
        foreach (var line in answer.ReplaceLineEndings("\n").Split('\n'))
        {
            if (CodeFencePattern().IsMatch(line))
                continue;

            var heading = HeadingPattern().Match(line);
            if (heading.Success)
            {
                AddSection();
                key = heading.Groups["key"].Value.Trim();
                lines.Clear();
                continue;
            }

            if (key is not null)
                lines.Add(line);
        }

        AddSection();
        return sections;

        void AddSection()
        {
            if (key is not null)
                sections.Add(new(key, string.Join('\n', lines).Trim()));
        }
    }

    /// <summary>
    /// Reads the work state of a step and the suggestions for its form from an answer of the model.
    /// </summary>
    /// <param name="answer">The answer of the model.</param>
    /// <param name="step">The step whose work state the model wrote.</param>
    /// <param name="form">The form of the step, whose empty fields may get suggestions.</param>
    /// <returns>What the lab read, with the problems of the sections.</returns>
    public static BflWorkStateAnswer Read(string answer, BflStep step, BflStepFormData form)
    {
        var expected = BflWorkStateSections.ForStep(step).Select(section => section.Key).ToList();
        var suggestable = SuggestableFields(step, form).ToDictionary(field => field.Id, StringComparer.Ordinal);
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        var suggestions = new Dictionary<string, string>(StringComparer.Ordinal);
        var problems = new List<string>();

        var parsed = Split(answer);
        foreach (var section in parsed)
        {
            if (section.Key.StartsWith(FIELD_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                var fieldId = section.Key[FIELD_PREFIX.Length..].Trim().ToLowerInvariant();
                if (suggestable.TryGetValue(fieldId, out var field) && !suggestions.ContainsKey(fieldId) && ValueOf(field, section.Text) is { } value)
                    suggestions[fieldId] = value;

                continue;
            }

            var key = section.Key.ToUpperInvariant();
            if (!expected.Contains(key))
                problems.Add($"Den Abschnitt [{section.Key}] gibt es in diesem Schritt nicht.");
            else if (!sections.TryAdd(key, section.Text))
                problems.Add($"Der Abschnitt [{key}] steht mehrmals in der Antwort.");
        }

        if (parsed.Count is 0)
            problems.Add("Die Antwort enthält keinen Abschnitt mit einer Überschrift der Form „## [SCHLÜSSEL] Titel“.");
        else
            problems.AddRange(expected.Where(key => !sections.ContainsKey(key)).Select(key => $"Der Abschnitt [{key}] fehlt."));

        return new BflWorkStateAnswer { Sections = sections, FieldSuggestions = suggestions, Problems = problems };
    }

    /// <summary>
    /// Gets the fields of a step for which the model may suggest a value: the empty fields with a text or a single
    /// choice.
    /// </summary>
    /// <remarks>
    /// A field which holds something personal gets no suggestion: the lab never ascribes such things to the person.
    /// A field with a single choice gets one, because the person may answer its question in the conversation, such as
    /// whether the event is public; the fields with several choices get none, since the person chooses quickly enough.
    /// </remarks>
    /// <param name="step">The step.</param>
    /// <param name="form">The form of the step.</param>
    /// <returns>The fields, in the order the form shows them.</returns>
    public static IReadOnlyList<BflFieldDefinition> SuggestableFields(BflStep step, BflStepFormData form) =>
        [.. BflFieldCatalog.ForStep(step).Where(field => field.Kind is BflFieldKind.SINGLE_LINE or BflFieldKind.MULTI_LINE or BflFieldKind.LINK or BflFieldKind.CHOICE && !field.IsPersonalDisclosure && string.IsNullOrWhiteSpace(form.Get(field.Id)))];

    /// <summary>
    /// Gets the option of a field with a single choice which a suggestion names, by its ID or its label.
    /// </summary>
    /// <remarks>
    /// The request lists the options by their labels, but the model may write the ID as well. Quotation marks and
    /// emphasis around the name are no part of it; anything else must match the name exactly, apart from case.
    /// </remarks>
    /// <param name="field">The field.</param>
    /// <param name="value">The suggested value.</param>
    /// <returns>The option, or null when the suggestion names none of the options of the field.</returns>
    private static BflFieldOption? OptionOf(BflFieldDefinition field, string value)
    {
        var name = value.Trim().Trim('"', '\'', '„', '“', '”', '‚', '‘', '’', '*', '.').Trim();
        return field.Options.FirstOrDefault(option => string.Equals(option.Id, name, StringComparison.OrdinalIgnoreCase) || string.Equals(option.Label, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the value a suggestion gives its field, if it fits.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The suggested value.</param>
    /// <returns>The value as the form stores it, such as the ID of the option chosen, or null when it does not fit.</returns>
    private static string? ValueOf(BflFieldDefinition field, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > field.Kind.MaxLength)
            return null;

        return field.Kind switch
        {
            BflFieldKind.SINGLE_LINE => value.Contains('\n') ? null : value,
            BflFieldKind.LINK => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? value : null,
            BflFieldKind.CHOICE => OptionOf(field, value)?.Id,

            _ => value,
        };
    }

    /// <summary>
    /// A heading with a key in brackets, such as "## [KEY] Title", "### [key]", or "## **[KEY] Title**".
    /// </summary>
    [GeneratedRegex(@"^\s{0,3}#{1,6}[ \t]*(?:\*\*)?[ \t]*\[(?<key>[^\]\n]+)\]")]
    private static partial Regex HeadingPattern();

    /// <summary>
    /// A line which opens or closes a code block, such as "```" or "```markdown".
    /// </summary>
    [GeneratedRegex(@"^\s*```[\w-]*\s*$")]
    private static partial Regex CodeFencePattern();
}