using System.Text;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Puts a session of the BeyondFacts Lab together for its research team: how the person worked with the lab, step by
/// step, without what would tell who the person is.
/// </summary>
/// <remarks>
/// <para>
/// A session holds the details of every step, its confirmed work state, its conversation as the person saw it, and the
/// confirmed materials the lab wrote. It leaves out everything the research team does not need and which would point
/// to the person: the name of the preparation, the content and the file names of the material, the texts of the
/// research on the web, which describe the event, the fields which pin down the event, drafts, and hidden messages. The
/// reflection folder stays out as well, since it only repeats the confirmed work states. AI Studio keeps no profile
/// and no path in a session, so none of them appears either. Personal disclosures stay out unless the person includes
/// them, and so does the conversation of a step which may contain some.
/// </para>
/// <para>
/// No model takes part, and the session is not anonymized yet: BflAnonymizer replaces the names and the other personal
/// details in it afterward. The texts therefore stay as they were written, without Markdown escapes, which would hide
/// an address or a phone number from the anonymizer. Every line ends with LF, on every system.
/// </para>
/// </remarks>
public static class BflShareModelBuilder
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflShareModelBuilder).Namespace, nameof(BflShareModelBuilder));

    /// <summary>
    /// Puts the session together.
    /// </summary>
    /// <param name="input">What the session is put together from.</param>
    /// <returns>The session, in Markdown.</returns>
    public static string Build(BflShareInput input)
    {
        var text = new StringBuilder();
        text.Append("# ").AppendLineLF(TB("BeyondFacts Lab: a shared session"));
        AppendAbout(text, input);

        foreach (var step in BflStep.All)
            AppendStep(text, step, input);

        return text.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// Appends what the research team needs to read the session: the version of AI Studio, the date, how much material
    /// the person added, and on which topics the lab researched.
    /// </summary>
    /// <param name="text">The session so far.</param>
    /// <param name="input">What the session is put together from.</param>
    private static void AppendAbout(StringBuilder text, BflShareInput input)
    {
        var documents = input.Materials.Count(material => material.Kind is BflMaterialKind.DOCUMENT);
        var recordings = input.Materials.Count(material => material.Kind is BflMaterialKind.RECORDING);
        var images = input.Materials.Count(material => material.Kind is BflMaterialKind.IMAGE);
        var topics = BflResearchTopic.All.Where(input.ResearchTopics.Contains).Select(topic => topic.Title).ToList();

        text.AppendLineLF().Append("## ").AppendLineLF(TB("About this session")).AppendLineLF();
        AppendItem(text, TB("Version of AI Studio"), input.AppVersion);
        AppendItem(text, TB("Shared on"), input.SharedAtUtc.ToLocalTime().ToString("d", input.Culture));
        AppendItem(text, TB("Material"), string.Format(input.Culture, TB("{0} documents, {1} recordings, {2} images"), documents, recordings, images));
        AppendItem(text, TB("Research on the web"), topics.Count > 0 ? string.Join(", ", topics) : TB("none"));
        AppendItem(text, TB("Personal disclosures"), input.IncludePersonalDisclosures ? TB("included") : TB("left out"));
    }

    /// <summary>
    /// Appends a step: its details, its confirmed work state, its conversation, and for the last step the confirmed
    /// materials.
    /// </summary>
    /// <param name="text">The session so far.</param>
    /// <param name="step">The step.</param>
    /// <param name="input">What the session is put together from.</param>
    private static void AppendStep(StringBuilder text, BflStep step, BflShareInput input)
    {
        text.AppendLineLF().Append("## ").AppendLineLF(step.Title);
        AppendDetails(text, step, input);
        AppendWorkState(text, step, input);
        AppendConversation(text, step, input);
        if (step is BflStep.RESULTS)
            AppendResults(text, input);
    }

    /// <summary>
    /// Appends the details of a step which the person entered, with the labels of the fields and options.
    /// </summary>
    /// <param name="text">The session so far.</param>
    /// <param name="step">The step.</param>
    /// <param name="input">What the session is put together from.</param>
    private static void AppendDetails(StringBuilder text, BflStep step, BflShareInput input)
    {
        text.AppendLineLF().Append("### ").AppendLineLF(TB("Details")).AppendLineLF();
        var form = input.Forms.GetValueOrDefault(step);
        var any = false;
        foreach (var field in BflFieldCatalog.ForStep(step))
        {
            if (form is null || field.IdentifiesEvent || field.IsPersonalDisclosure && !input.IncludePersonalDisclosures)
                continue;

            var value = ValueOf(field, form);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            any = true;
            if (field.Kind is BflFieldKind.MULTI_LINE)
                text.Append("**").Append(field.Label).AppendLineLF(":**").AppendLineLF().AppendLineLF(WithLineBreaks(value)).AppendLineLF();
            else
                AppendItem(text, field.Label, value);
        }

        if (!any)
            text.Append('_').Append(TB("No details.")).AppendLineLF("_");
    }

    /// <summary>
    /// Appends the confirmed work state of a step, with all of its sections.
    /// </summary>
    /// <param name="text">The session so far.</param>
    /// <param name="step">The step.</param>
    /// <param name="input">What the session is put together from.</param>
    private static void AppendWorkState(StringBuilder text, BflStep step, BflShareInput input)
    {
        text.AppendLineLF().Append("### ").AppendLineLF(TB("Confirmed work state")).AppendLineLF();
        if (input.WorkStates.GetValueOrDefault(step)?.Confirmed is not { } confirmed)
        {
            text.Append('_').Append(TB("No confirmed work state.")).AppendLineLF("_");
            return;
        }

        var any = false;
        foreach (var section in BflWorkStateSections.ForStep(step))
        {
            if (section.IsPersonalDisclosure && !input.IncludePersonalDisclosures || !confirmed.Sections.TryGetValue(section.Key, out var sectionText) || string.IsNullOrWhiteSpace(sectionText))
                continue;

            any = true;
            text.Append("#### ").AppendLineLF(section.Title).AppendLineLF().AppendLineLF(WithLineBreaks(sectionText)).AppendLineLF();
        }

        if (!any)
            text.Append('_').Append(TB("The confirmed work state holds nothing to share.")).AppendLineLF("_");
    }

    /// <summary>
    /// Appends the conversation of a step as the person saw it.
    /// </summary>
    /// <param name="text">The session so far.</param>
    /// <param name="step">The step.</param>
    /// <param name="input">What the session is put together from.</param>
    private static void AppendConversation(StringBuilder text, BflStep step, BflShareInput input)
    {
        text.AppendLineLF().Append("### ").AppendLineLF(TB("Conversation")).AppendLineLF();
        if (!input.Conversations.TryGetValue(step, out var messages) || messages.Count is 0)
        {
            text.Append('_').Append(TB("No conversation.")).AppendLineLF("_");
            return;
        }

        if (!input.IncludePersonalDisclosures && BflReflectionFolder.MayHoldPersonalDisclosures(step))
        {
            text.Append('_').Append(TB("Left out, as it may contain personal disclosures.")).AppendLineLF("_");
            return;
        }

        foreach (var message in messages)
        {
            var speaker = message.IsFromPerson ? TB("Person") : TB("Lab");
            text.Append("**").Append(speaker).AppendLineLF(":**").AppendLineLF().AppendLineLF(WithLineBreaks(message.Text)).AppendLineLF();
        }
    }

    /// <summary>
    /// Appends the confirmed versions of the materials the lab wrote. The reflection folder stays out, since it only
    /// repeats the confirmed work states.
    /// </summary>
    /// <param name="text">The session so far.</param>
    /// <param name="input">What the session is put together from.</param>
    private static void AppendResults(StringBuilder text, BflShareInput input)
    {
        foreach (var kind in BflResultKind.All.Where(kind => kind.IsWrittenByModel))
        {
            text.AppendLineLF().Append("### ").AppendLineLF(kind.Title).AppendLineLF();
            if (input.Results.GetValueOrDefault(kind)?.Confirmed is { } confirmed && !string.IsNullOrWhiteSpace(confirmed.Text))
                text.AppendLineLF(WithLineBreaks(confirmed.Text));
            else
                text.Append('_').Append(TB("Not confirmed.")).AppendLineLF("_");
        }
    }

    /// <summary>
    /// Appends a line of a list with a label in bold.
    /// </summary>
    /// <param name="text">The session so far.</param>
    /// <param name="label">The label.</param>
    /// <param name="value">The value, in one line.</param>
    private static void AppendItem(StringBuilder text, string label, string value) => text.Append("- **").Append(label).Append(":** ").AppendLineLF(value.Trim().ReplaceLineEndings(" "));

    /// <summary>
    /// Gets the value of a field as the person sees it: the labels of the options chosen, or the text entered.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="form">The form of the step of the field.</param>
    /// <returns>The value, empty when the field has none or only an option this version does not know.</returns>
    private static string ValueOf(BflFieldDefinition field, BflStepFormData form) => field.Kind switch
    {
        BflFieldKind.CHOICE => field.Options.FirstOrDefault(option => option.Id == form.Get(field.Id))?.Label ?? string.Empty,
        BflFieldKind.MULTI_CHOICE => string.Join(", ", field.Options.Where(option => form.GetChoices(field.Id).Contains(option.Id)).Select(option => option.Label)),

        _ => form.Get(field.Id),
    };

    /// <summary>
    /// Gets a text which the person or a model wrote, trimmed and with LF line breaks.
    /// </summary>
    /// <param name="text">The text.</param>
    private static string WithLineBreaks(string text) => text.Trim().ReplaceLineEndings("\n");
}