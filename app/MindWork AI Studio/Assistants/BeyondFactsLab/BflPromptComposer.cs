using System.Text;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Puts together the system prompt of a step of the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The same parts and forms always give the same prompt, byte by byte, on every system. The parts which stay the
/// same during a conversation come first, the core and the guide, and the details last, so that providers which
/// cache the beginning of a prompt can do so. The texts this class adds itself are German, like the prompts.
/// </remarks>
public static class BflPromptComposer
{
    /// <summary>
    /// The line break of the prompt, the same on every system.
    /// </summary>
    private const char LINE_BREAK = '\n';

    /// <summary>
    /// Puts together the system prompt of a step.
    /// </summary>
    /// <param name="parts">The texts of the prompt.</param>
    /// <param name="step">The step the chat belongs to.</param>
    /// <param name="forms">The forms of the preparation, by their steps; a step this version cannot open is missing.</param>
    /// <param name="responseLanguage">The name of the language the model answers in, e.g., the language of the app.</param>
    /// <returns>The system prompt.</returns>
    /// <exception cref="FormatException">The core prompt has a placeholder without a value.</exception>
    public static string Compose(BflPromptParts parts, BflStep step, IReadOnlyDictionary<BflStep, BflStepFormData> forms, string responseLanguage) => BflPromptTemplate.Fill(parts.Core, new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [BflPromptTemplate.RESPONSE_LANGUAGE] = responseLanguage,
        [BflPromptTemplate.ADDRESS_FORM] = parts.Address.Trim(),
        [BflPromptTemplate.KNOWLEDGE] = parts.Knowledge.Trim(),
        [BflPromptTemplate.STEP] = parts.Step.Trim(),
        [BflPromptTemplate.DETAILS] = Details(step, forms),
        [BflPromptTemplate.MISSING_DETAILS] = MissingDetails(step, forms)
    }).ReplaceLineEndings(LINE_BREAK.ToString());

    /// <summary>
    /// Lists what the person entered into the forms, step by step.
    /// </summary>
    /// <remarks>
    /// Something the person confided, such as their personal involvement, reaches only the chat of its own step:
    /// no other step may confront them with it by surprise.
    /// </remarks>
    /// <param name="step">The step the chat belongs to.</param>
    /// <param name="forms">The forms of the preparation, by their steps.</param>
    private static string Details(BflStep step, IReadOnlyDictionary<BflStep, BflStepFormData> forms)
    {
        var text = new StringBuilder();
        foreach (var candidate in BflStep.All)
        {
            if (!forms.TryGetValue(candidate, out var form))
                continue;

            var lines = new List<string>();
            foreach (var field in BflFieldCatalog.ForStep(candidate))
            {
                if (field.IsPersonalDisclosure && candidate != step)
                    continue;

                var value = ValueOf(field, form);
                if (value.Length is 0)
                    continue;

                var marker = field.IsPersonalDisclosure ? " (persönliche Angabe)" : string.Empty;
                lines.Add($"- {field.Label}{marker}: {value.Replace("\n", "\n  ", StringComparison.Ordinal)}");
            }

            if (lines.Count is 0)
                continue;

            if (text.Length > 0)
                text.Append(LINE_BREAK);

            text.Append("## ").Append(StepHeading(candidate, step)).Append(LINE_BREAK);
            foreach (var line in lines)
                text.Append(line).Append(LINE_BREAK);
        }

        return text.Length is 0 ? "Die Person hat noch keine Angaben gemacht." : text.ToString().TrimEnd();
    }

    /// <summary>
    /// Lists the key details the forms still lack, step by step.
    /// </summary>
    /// <param name="step">The step the chat belongs to.</param>
    /// <param name="forms">The forms of the preparation, by their steps; a missing form counts as empty.</param>
    private static string MissingDetails(BflStep step, IReadOnlyDictionary<BflStep, BflStepFormData> forms)
    {
        var lines = new List<string>();
        foreach (var candidate in BflStep.All)
        {
            var missing = BflMissingDetails.Of(candidate, forms.GetValueOrDefault(candidate) ?? new BflStepFormData());
            if (missing.Count > 0)
                lines.Add($"- {StepHeading(candidate, step)}: {string.Join("; ", missing.Select(field => field.Label))}");
        }

        return lines.Count is 0 ? "Es fehlen keine wichtigen Angaben." : $"Diese wichtigen Angaben fehlen noch:{LINE_BREAK}{string.Join(LINE_BREAK, lines)}";
    }

    /// <summary>
    /// Gets the value of a field as the model reads it: the labels of chosen options, or the text with its lines.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="form">The form which holds the value.</param>
    /// <returns>The value, empty when the field has none.</returns>
    private static string ValueOf(BflFieldDefinition field, BflStepFormData form)
    {
        if (field.Kind is BflFieldKind.CHOICE or BflFieldKind.MULTI_CHOICE)
        {
            // An option this version does not know, e.g., one a newer version added, has no label to show:
            var chosen = form.GetChoices(field.Id);
            return string.Join(", ", field.Options.Where(option => chosen.Contains(option.Id)).Select(option => option.Label));
        }

        return form.Get(field.Id).Trim().ReplaceLineEndings(LINE_BREAK.ToString());
    }

    /// <summary>
    /// Gets the heading of a step in the details, which marks the step the chat belongs to.
    /// </summary>
    /// <param name="candidate">The step to name.</param>
    /// <param name="step">The step the chat belongs to.</param>
    private static string StepHeading(BflStep candidate, BflStep step) => candidate == step ? $"{candidate.Title} (dieser Schritt)" : candidate.Title;
}