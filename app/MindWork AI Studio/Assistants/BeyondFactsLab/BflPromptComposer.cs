using System.Globalization;
using System.Text;

using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Puts together the system prompts of the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The same parts, forms, and materials always give the same prompt, byte by byte, on every system. The parts which
/// stay the same during a conversation come first, the core and the guide, then the material, which changes seldom,
/// and the details last, so that providers which cache the beginning of a prompt can do so. The texts this class adds
/// itself are German, like the prompts.
/// </remarks>
public static class BflPromptComposer
{
    /// <summary>
    /// The line break of the prompt, the same on every system.
    /// </summary>
    private const char LINE_BREAK = '\n';

    /// <summary>
    /// What a prompt says about the tools when nobody checked them: none of them is available.
    /// </summary>
    private static readonly BflResearchAvailability NO_TOOLS = new(ToolOfferBlockReason.NOT_AVAILABLE_HERE, ToolOfferBlockReason.NOT_AVAILABLE_HERE);

    /// <summary>
    /// Puts together the system prompt of a step.
    /// </summary>
    /// <param name="parts">The texts of the prompt.</param>
    /// <param name="step">The step the chat belongs to.</param>
    /// <param name="forms">The forms of the preparation, by their steps; a step this version cannot open is missing.</param>
    /// <param name="materials">What the prompt carries of the material of the preparation, see BflMaterialBudget.</param>
    /// <param name="responseLanguage">The name of the language the model answers in, e.g., the language of the app.</param>
    /// <param name="workStates">The work states of the preparation, by their steps; only the confirmed ones reach the prompt. Null when there are none.</param>
    /// <param name="tools">What keeps the tools of the lab from being offered with the provider of the preparation; null counts as no tools at all.</param>
    /// <param name="research">The research of the preparation, by its topics; only the step "Strategy" gets it. Null when there is none.</param>
    /// <returns>The system prompt.</returns>
    /// <exception cref="FormatException">The core prompt has a placeholder without a value.</exception>
    public static string Compose(BflPromptParts parts, BflStep step, IReadOnlyDictionary<BflStep, BflStepFormData> forms, IReadOnlyList<BflMaterialExcerpt> materials, string responseLanguage, IReadOnlyDictionary<BflStep, BflWorkState>? workStates = null, BflResearchAvailability? tools = null, IReadOnlyDictionary<BflResearchTopic, BflResearch>? research = null) => BflPromptTemplate.Fill(parts.Core, new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [BflPromptTemplate.RESPONSE_LANGUAGE] = responseLanguage,
        [BflPromptTemplate.ADDRESS_FORM] = parts.Address.Trim(),
        [BflPromptTemplate.KNOWLEDGE] = parts.Knowledge.Trim(),
        [BflPromptTemplate.STEP] = parts.Step.Trim(),
        [BflPromptTemplate.MATERIALS] = Materials(materials),
        [BflPromptTemplate.TOOLS] = BflToolStatus.ForStep(tools ?? NO_TOOLS).Trim(),
        [BflPromptTemplate.RESEARCH] = Research(step, research ?? new Dictionary<BflResearchTopic, BflResearch>(), forms),
        [BflPromptTemplate.WORK_STATES] = WorkStates(step, workStates ?? new Dictionary<BflStep, BflWorkState>(), forms),
        [BflPromptTemplate.DETAILS] = Details(step, forms),
        [BflPromptTemplate.MISSING_DETAILS] = MissingDetails(step, forms)
    }).ReplaceLineEndings(LINE_BREAK.ToString());

    /// <summary>
    /// Puts together the system prompt of the conversation about whether the lab fits a task.
    /// </summary>
    /// <remarks>
    /// It knows no preparation and leaves out the guide, so that it stays short.
    /// </remarks>
    /// <param name="parts">The texts of the prompt.</param>
    /// <param name="responseLanguage">The name of the language the model answers in, e.g., the language of the app.</param>
    /// <param name="tools">What keeps the tools of the lab from being offered with the provider of the conversation; null counts as no tools at all.</param>
    /// <returns>The system prompt.</returns>
    /// <exception cref="FormatException">The prompt has a placeholder without a value.</exception>
    public static string ComposeOnboarding(BflOnboardingPromptParts parts, string responseLanguage, BflResearchAvailability? tools = null) => BflPromptTemplate.Fill(parts.Onboarding, new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [BflPromptTemplate.RESPONSE_LANGUAGE] = responseLanguage,
        [BflPromptTemplate.ADDRESS_FORM] = parts.Address.Trim(),
        [BflPromptTemplate.TOOLS] = BflToolStatus.ForOnboarding(tools ?? NO_TOOLS).Trim()
    }).ReplaceLineEndings(LINE_BREAK.ToString());

    /// <summary>
    /// Puts together the request which lets the model write the work state of a step, at the end of its conversation.
    /// </summary>
    /// <param name="template">The request with its placeholders, see BflPromptLibrary.WORK_STATE_PATH.</param>
    /// <param name="step">The step whose work state the model writes.</param>
    /// <param name="form">The form of the step, whose empty fields may get suggestions.</param>
    /// <returns>The request.</returns>
    /// <exception cref="FormatException">The request has a placeholder without a value.</exception>
    public static string ComposeWorkStateRequest(string template, BflStep step, BflStepFormData form) => BflPromptTemplate.Fill(template, new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [BflPromptTemplate.SECTIONS] = WorkStateSections(step),
        [BflPromptTemplate.FIELDS] = SuggestableFields(step, form)
    }).ReplaceLineEndings(LINE_BREAK.ToString()).Trim();

    /// <summary>
    /// Puts together the request which lets the model write a result, at the end of a hidden copy of the conversation
    /// of the last step.
    /// </summary>
    /// <param name="template">The request, see BflPromptLibrary.ResultPath.</param>
    /// <param name="workStates">The work states of the preparation, by their steps, to name the steps without a confirmed one.</param>
    /// <returns>The request.</returns>
    /// <exception cref="FormatException">The request has a placeholder without a value.</exception>
    public static string ComposeResultRequest(string template, IReadOnlyDictionary<BflStep, BflWorkState> workStates) => BflPromptTemplate.Fill(template, new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [BflPromptTemplate.MISSING_WORK_STATES] = MissingWorkStates(workStates)
    }).ReplaceLineEndings(LINE_BREAK.ToString()).Trim();

    /// <summary>
    /// Puts together the system prompt of a research agent.
    /// </summary>
    /// <remarks>
    /// It carries nothing but the public details about the event: no other detail, no material, no work state, and no
    /// conversation of the preparation, so that none of them can get into a query to the web search.
    /// </remarks>
    /// <param name="parts">The texts of the prompt.</param>
    /// <param name="details">The public details about the event, see PublicDetailsOf.</param>
    /// <param name="responseLanguage">The name of the language the agent writes in, e.g., the language of the app.</param>
    /// <returns>The system prompt.</returns>
    /// <exception cref="FormatException">The prompt has a placeholder without a value.</exception>
    public static string ComposeResearch(BflResearchPromptParts parts, IReadOnlyList<BflResearchDetail> details, string responseLanguage) => BflPromptTemplate.Fill(parts.Research, new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [BflPromptTemplate.RESPONSE_LANGUAGE] = responseLanguage,
        [BflPromptTemplate.TOPIC] = parts.Topic.Trim(),
        [BflPromptTemplate.TOOL_CALLS] = ToolSelectionRules.MAX_TOOL_CALLS.ToString(CultureInfo.InvariantCulture),
        [BflPromptTemplate.PUBLIC_DETAILS] = PublicDetails(details)
    }).ReplaceLineEndings(LINE_BREAK.ToString());

    /// <summary>
    /// Gets the public details about the event from the forms of a preparation: the only details a research receives.
    /// </summary>
    /// <remarks>
    /// Only fields marked with BflFieldDefinition.IsSharedWithResearch count, in the order of the catalog, and only
    /// those with a value.
    /// </remarks>
    /// <param name="forms">The forms of the preparation, by their steps; a missing form counts as empty.</param>
    /// <returns>The public details, as a research receives them.</returns>
    public static IReadOnlyList<BflResearchDetail> PublicDetailsOf(IReadOnlyDictionary<BflStep, BflStepFormData> forms)
    {
        List<BflResearchDetail> details = [];
        foreach (var field in BflFieldCatalog.FIELDS.Where(field => field.IsSharedWithResearch))
        {
            if (!forms.TryGetValue(field.Step, out var form))
                continue;

            var value = ValueOf(field, form);
            if (value.Length > 0)
                details.Add(new BflResearchDetail { FieldId = field.Id, Value = value });
        }

        return details;
    }

    /// <summary>
    /// Lists what the research agents found, for the step "Strategy".
    /// </summary>
    /// <remarks>
    /// The other steps leave the research out: what the person takes over from it, such as the participants, reaches
    /// them through the confirmed work state of the step "Strategy". A research whose public details changed since is
    /// marked, so that the model checks it with the person. The research comes in the order of its topics, each with
    /// the day it was done. For an event which is not public, an earlier research no longer counts: the person said
    /// that its details are not public, see BflEventVisibility.
    /// </remarks>
    /// <param name="step">The step the chat belongs to.</param>
    /// <param name="research">The research of the preparation, by its topics.</param>
    /// <param name="forms">The forms of the preparation, by their steps.</param>
    private static string Research(BflStep step, IReadOnlyDictionary<BflResearchTopic, BflResearch> research, IReadOnlyDictionary<BflStep, BflStepFormData> forms)
    {
        if (step is not BflStep.STRATEGY)
            return "Die Recherche nutzt der Schritt „Strategie“. Was die Person daraus übernimmt, steht in dessen bestätigtem Arbeitsstand.";

        if (BflEventVisibility.IsNotPublic(forms))
            return "Die Veranstaltung ist nicht öffentlich. Deshalb recherchiert das Lab nicht im Web, und es gibt keine Recherche.";

        var details = PublicDetailsOf(forms);
        var text = new StringBuilder();
        foreach (var topic in BflResearchTopic.All)
        {
            if (!research.TryGetValue(topic, out var found) || found.Text.Trim().Length is 0)
                continue;

            if (text.Length > 0)
                text.Append(LINE_BREAK);

            text.Append($"<thema titel=\"{topic.Title}\" stand=\"{found.CreatedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}\">").Append(LINE_BREAK);
            if (found.IsOutdated(details))
                text.Append("(Die öffentlichen Angaben haben sich seit dieser Recherche geändert. Sie passt womöglich nicht mehr zur Veranstaltung.)").Append(LINE_BREAK);

            text.Append(found.Text.Trim().ReplaceLineEndings(LINE_BREAK.ToString())).Append(LINE_BREAK).Append("</thema>").Append(LINE_BREAK);
        }

        return text.Length is 0 ? "Es gibt noch keine Recherche." : text.ToString().TrimEnd();
    }

    /// <summary>
    /// Lists the public details about the event for a research agent.
    /// </summary>
    /// <param name="details">The public details.</param>
    private static string PublicDetails(IReadOnlyList<BflResearchDetail> details)
    {
        var lines = new List<string>();
        foreach (var detail in details)
        {
            // A detail whose field is not public, or which this version does not know, never reaches a research:
            if (BflFieldCatalog.FIELDS.FirstOrDefault(candidate => string.Equals(candidate.Id, detail.FieldId, StringComparison.Ordinal)) is not { IsSharedWithResearch: true } field)
                continue;

            lines.Add($"- {field.Label}: {detail.Value.Replace("\n", "\n  ", StringComparison.Ordinal)}");
        }

        return lines.Count is 0 ? "Die Person hat noch keine öffentlichen Angaben gemacht." : string.Join(LINE_BREAK, lines);
    }

    /// <summary>
    /// Names the steps without a confirmed work state, in their order.
    /// </summary>
    /// <param name="workStates">The work states of the preparation, by their steps.</param>
    private static string MissingWorkStates(IReadOnlyDictionary<BflStep, BflWorkState> workStates)
    {
        var missing = BflStep.All.Where(step => workStates.GetValueOrDefault(step)?.Confirmed is null).Select(step => $"„{step.Title}“").ToList();
        return missing.Count is 0 ? "keine; alle Schritte sind bestätigt." : $"{string.Join(", ", missing)}.";
    }

    /// <summary>
    /// Puts together the request which asks the model once to correct the sections of a work state.
    /// </summary>
    /// <param name="problems">What is wrong with the sections, see BflWorkStateAnswer.Problems.</param>
    /// <returns>The request.</returns>
    public static string ComposeWorkStateRepairRequest(IReadOnlyList<string> problems)
    {
        var text = new StringBuilder("Das Lab konnte deinen Arbeitsstand nicht lesen:").Append(LINE_BREAK);
        foreach (var problem in problems)
            text.Append("- ").Append(problem).Append(LINE_BREAK);

        return text.Append(LINE_BREAK).Append("Schreibe den Arbeitsstand noch einmal vollständig, mit allen Abschnitten in der verlangten Form, und sonst nichts.").ToString();
    }

    /// <summary>
    /// Lists the sections of the work state of a step with their headings, as the model writes them.
    /// </summary>
    /// <param name="step">The step.</param>
    private static string WorkStateSections(BflStep step)
    {
        var text = new StringBuilder();
        foreach (var section in BflWorkStateSections.ForStep(step))
        {
            if (text.Length > 0)
                text.Append(LINE_BREAK);

            text.Append($"## [{section.Key}] {section.Title}").Append(LINE_BREAK);
            text.Append(section.Description).Append(LINE_BREAK);
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Lists the empty fields of the form of a step for which the model may suggest values.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="form">The form of the step.</param>
    private static string SuggestableFields(BflStep step, BflStepFormData form)
    {
        var fields = BflMarkdownSections.SuggestableFields(step, form);
        if (fields.Count is 0)
            return "Die Person hat alle Felder dieses Schritts ausgefüllt, für die du Werte vorschlagen könntest. Schreibe deshalb keine Vorschläge.";

        var text = new StringBuilder();
        text.Append("Die folgenden Felder im Formular „Ihre Angaben“ dieses Schritts sind noch leer. Ergibt sich der Wert eines Felds eindeutig aus dem Gespräch, den Angaben oder dem Material, schreibe ihn nach den Abschnitten unter eine eigene Überschrift, etwa „## [")
            .Append(BflMarkdownSections.FIELD_PREFIX).Append(fields[0].Id).Append("] ").Append(fields[0].Label).Append("“. Lass jedes Feld weg, für das es keinen eindeutigen Wert gibt, und rate nie. Die Person übernimmt jeden Vorschlag selbst.").Append(LINE_BREAK).Append(LINE_BREAK);

        foreach (var field in fields)
        {
            var kind = field.Kind switch
            {
                BflFieldKind.SINGLE_LINE => $"eine Zeile, höchstens {field.Kind.MaxLength} Zeichen",
                BflFieldKind.LINK => "eine vollständige Webadresse, die mit https:// oder http:// beginnt",
                BflFieldKind.CHOICE => $"genau eine dieser Optionen, so geschrieben: {string.Join(", ", field.Options.Select(option => $"„{option.Label}“"))}",

                _ => $"Text, höchstens {field.Kind.MaxLength} Zeichen",
            };

            text.Append($"- [{BflMarkdownSections.FIELD_PREFIX}{field.Id}] {field.Label} ({kind})").Append(LINE_BREAK);
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Lists the material of the preparation, each with its name and as much of its text as fits, or for an image,
    /// whether it goes along.
    /// </summary>
    /// <remarks>
    /// The model learns which material is shortened, so that it neither takes the beginning for the whole nor
    /// guesses what follows, and which text is the transcript of a recording, whose spelling may be off.
    /// </remarks>
    /// <param name="materials">What the prompt carries of each material, in the order the person added them.</param>
    private static string Materials(IReadOnlyList<BflMaterialExcerpt> materials)
    {
        if (materials.Count is 0)
            return "Die Person hat kein Material hinzugefügt.";

        var text = new StringBuilder();
        for (var index = 0; index < materials.Count; index++)
        {
            var excerpt = materials[index];
            var material = excerpt.Share.Material;
            if (index > 0)
                text.Append(LINE_BREAK);

            text.Append($"## Material {index + 1}: „{material.Name}“").Append(LINE_BREAK);
            if (material.Kind is BflMaterialKind.IMAGE)
            {
                text.Append(excerpt.Share.Image switch
                {
                    BflImageDelivery.SENT => "(Ein Bild. Es liegt der ersten Nachricht dieses Gesprächs bei.)",
                    BflImageDelivery.OVER_THE_LIMIT => "(Ein Bild. Das Modell nimmt nicht so viele Bilder auf einmal an. Du kennst nur seinen Namen.)",
                    _ => "(Ein Bild. Das Modell kann keine Bilder sehen. Du kennst nur seinen Namen.)",
                }).Append(LINE_BREAK);
                continue;
            }

            if (material.Kind is BflMaterialKind.RECORDING)
                text.Append("(Das Transkript einer Aufnahme. Namen und Fachbegriffe können falsch geschrieben sein.)").Append(LINE_BREAK);

            if (excerpt.Text.Length is 0)
            {
                text.Append("(Für dieses Material reicht der Platz im Kontext des Modells nicht. Du kennst nur seinen Namen.)").Append(LINE_BREAK);
                continue;
            }

            if (material.IsShortened)
                text.Append("(Das Lab hat nur den Anfang dieses Materials übernommen. Den Rest kennst du nicht.)").Append(LINE_BREAK);

            if (!excerpt.Share.IsComplete)
                text.Append($"(Gekürzt: Hier steht nur der Anfang, etwa {excerpt.Share.Percent} % des Materials. Für mehr reicht der Platz im Kontext des Modells nicht.)").Append(LINE_BREAK);

            text.Append(LINE_BREAK).Append(excerpt.Text.Trim()).Append(LINE_BREAK);
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Lists the work states the person confirmed, step by step, each with the sections which hold text.
    /// </summary>
    /// <remarks>
    /// A draft never reaches a prompt: the person has not checked it yet. Like a personal field of a form, a personal
    /// section reaches only the chat of its own step. A section this version does not know, e.g., one a newer version
    /// added, has no title and is left out.
    /// </remarks>
    /// <param name="step">The step the chat belongs to.</param>
    /// <param name="workStates">The work states of the preparation, by their steps.</param>
    /// <param name="forms">The forms of the preparation, by their steps, which tell whether a work state is outdated.</param>
    private static string WorkStates(BflStep step, IReadOnlyDictionary<BflStep, BflWorkState> workStates, IReadOnlyDictionary<BflStep, BflStepFormData> forms)
    {
        var text = new StringBuilder();
        foreach (var candidate in BflStep.All)
        {
            if (workStates.GetValueOrDefault(candidate)?.Confirmed is not { } confirmed)
                continue;

            var sections = new StringBuilder();
            foreach (var section in BflWorkStateSections.ForStep(candidate))
            {
                if (section.IsPersonalDisclosure && candidate != step)
                    continue;

                var value = confirmed.Sections.GetValueOrDefault(section.Key, string.Empty).Trim().ReplaceLineEndings(LINE_BREAK.ToString());
                if (value.Length is 0)
                    continue;

                var marker = section.IsPersonalDisclosure ? " (persönliche Angabe)" : string.Empty;
                sections.Append("### ").Append(section.Title).Append(marker).Append(LINE_BREAK).Append(value).Append(LINE_BREAK);
            }

            if (sections.Length is 0)
                continue;

            if (text.Length > 0)
                text.Append(LINE_BREAK);

            var outdated = BflStaleness.Of(candidate, workStates, forms) is BflWorkStateStatus.OUTDATED ? " (veraltet)" : string.Empty;
            text.Append("## ").Append(StepHeading(candidate, step)).Append(outdated).Append(LINE_BREAK).Append(sections);
        }

        return text.Length is 0 ? "Die Person hat noch keinen Arbeitsstand bestätigt." : text.ToString().TrimEnd();
    }

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