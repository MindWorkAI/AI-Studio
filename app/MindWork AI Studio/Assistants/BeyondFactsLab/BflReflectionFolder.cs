using System.Text;

using AIStudio.Chat;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Puts the reflection folder of a preparation together: all confirmed work states, the open questions, the material,
/// and, if the person likes, the conversations of the steps.
/// </summary>
/// <remarks>
/// No model takes part. The person confirmed every work state word for word, so the folder repeats them word for word
/// instead of having a model rewrite them. A step without a confirmed work state is named, not filled. Personal
/// disclosures stay out unless the person includes them, and so does the conversation of a step which may contain
/// some: the folder is meant to be passed on.
/// </remarks>
public static class BflReflectionFolder
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflReflectionFolder).Namespace, nameof(BflReflectionFolder));

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflReflectionFolder).Namespace, nameof(BflReflectionFolder));

    /// <summary>
    /// The key of the section of every step which holds its open points; the folder gathers them in one place.
    /// </summary>
    private const string OPEN_POINTS_KEY = "OPEN_POINTS";

    /// <summary>
    /// Puts the reflection folder together.
    /// </summary>
    /// <param name="input">What the folder is put together from.</param>
    /// <returns>The folder, in Markdown.</returns>
    public static string Build(BflReflectionFolderInput input)
    {
        var text = new StringBuilder();
        text.Append("# ").Append(Markdown.EscapeInlineText(BflResultKind.REFLECTION_FOLDER.Title)).Append(": ").AppendLine(Markdown.EscapeInlineText(input.ProjectName));

        foreach (var step in BflStep.All)
            AppendStep(text, step, input);

        AppendOpenPoints(text, input);
        AppendMaterials(text, input);
        if (input.Conversations.Count > 0)
            AppendConversations(text, input);

        return text.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// Gets the messages of a conversation as the person saw them.
    /// </summary>
    /// <remarks>
    /// Hidden messages stay out, such as the request with which the lab opens a conversation, and so does everything
    /// but text.
    /// </remarks>
    /// <param name="thread">The conversation.</param>
    /// <returns>The visible messages with text, in their order.</returns>
    public static IReadOnlyList<BflConversationMessage> MessagesOf(ChatThread thread) =>
    [
        .. thread.Blocks
            .Where(block => !block.HideFromUser && block.Role is ChatRole.USER or ChatRole.AI)
            .Select(block => block.Content is ContentText { Text: var messageText } && !string.IsNullOrWhiteSpace(messageText) ? new BflConversationMessage(block.Role is ChatRole.USER, messageText.Trim()) : null)
            .OfType<BflConversationMessage>()
    ];

    /// <summary>
    /// Tells whether a step may hold personal disclosures, in its work state or in its form, so that its conversation
    /// may contain them too.
    /// </summary>
    /// <param name="step">The step.</param>
    public static bool MayHoldPersonalDisclosures(BflStep step) => BflWorkStateSections.ForStep(step).Any(section => section.IsPersonalDisclosure) || BflFieldCatalog.ForStep(step).Any(field => field.IsPersonalDisclosure);

    /// <summary>
    /// Appends the confirmed work state of a step, without its open points.
    /// </summary>
    /// <param name="text">The folder so far.</param>
    /// <param name="step">The step.</param>
    /// <param name="input">What the folder is put together from.</param>
    private static void AppendStep(StringBuilder text, BflStep step, BflReflectionFolderInput input)
    {
        text.AppendLine().Append("## ").AppendLine(Markdown.EscapeInlineText(step.Title)).AppendLine();
        if (input.WorkStates.GetValueOrDefault(step)?.Confirmed is not { } confirmed)
        {
            text.Append('_').Append(Markdown.EscapeInlineText(TB("No confirmed work state."))).AppendLine("_");
            return;
        }

        var confirmedAt = confirmed.ConfirmedAtUtc.ToLocalTime().ToString("d", input.Culture);
        text.Append('_').Append(Markdown.EscapeInlineText(string.Format(input.Culture, TB("Version {0}, confirmed on {1}."), confirmed.Revision, confirmedAt))).AppendLine("_");
        var notice = BflStaleness.Of(step, input.WorkStates, input.Forms) switch
        {
            BflWorkStateStatus.OUTDATED => TB("An earlier step changed after this confirmation."),
            BflWorkStateStatus.DETAILS_CHANGED => TB("The details of this step changed after this confirmation."),
            _ => string.Empty
        };

        if (notice.Length > 0)
            text.AppendLine().Append('_').Append(Markdown.EscapeInlineText(notice)).AppendLine("_");

        foreach (var section in BflWorkStateSections.ForStep(step))
        {
            if (section.Key is OPEN_POINTS_KEY || section.IsPersonalDisclosure && !input.IncludePersonalDisclosures || !confirmed.Sections.TryGetValue(section.Key, out var sectionText) || string.IsNullOrWhiteSpace(sectionText))
                continue;

            text.AppendLine().Append("### ").AppendLine(Markdown.EscapeInlineText(section.Title)).AppendLine().AppendLine(sectionText.Trim());
        }
    }

    /// <summary>
    /// Appends the open points of all confirmed work states, by their steps.
    /// </summary>
    /// <param name="text">The folder so far.</param>
    /// <param name="input">What the folder is put together from.</param>
    private static void AppendOpenPoints(StringBuilder text, BflReflectionFolderInput input)
    {
        text.AppendLine().Append("## ").AppendLine(Markdown.EscapeInlineText(TB("Open questions"))).AppendLine();
        var any = false;
        foreach (var step in BflStep.All)
        {
            if (input.WorkStates.GetValueOrDefault(step)?.Confirmed?.Sections.GetValueOrDefault(OPEN_POINTS_KEY) is not { } openPoints || string.IsNullOrWhiteSpace(openPoints))
                continue;

            any = true;
            text.Append("### ").AppendLine(Markdown.EscapeInlineText(step.Title)).AppendLine().AppendLine(openPoints.Trim()).AppendLine();
        }

        if (!any)
            text.Append('_').Append(Markdown.EscapeInlineText(TB("No confirmed work state names open questions."))).AppendLine("_");
    }

    /// <summary>
    /// Appends the list of the material.
    /// </summary>
    /// <param name="text">The folder so far.</param>
    /// <param name="input">What the folder is put together from.</param>
    private static void AppendMaterials(StringBuilder text, BflReflectionFolderInput input)
    {
        text.AppendLine().Append("## ").AppendLine(Markdown.EscapeInlineText(TB("Material"))).AppendLine();
        if (input.Materials.Count is 0)
        {
            text.Append('_').Append(Markdown.EscapeInlineText(TB("No material was added."))).AppendLine("_");
            return;
        }

        foreach (var material in input.Materials.OrderBy(material => material.AddedAtUtc))
            text.Append("- ").Append(Markdown.EscapeInlineText(material.Name)).Append(" (").Append(Markdown.EscapeInlineText(KindName(material.Kind))).AppendLine(")");
    }

    /// <summary>
    /// Appends the conversations of the steps as an appendix.
    /// </summary>
    /// <param name="text">The folder so far.</param>
    /// <param name="input">What the folder is put together from.</param>
    private static void AppendConversations(StringBuilder text, BflReflectionFolderInput input)
    {
        text.AppendLine().Append("## ").AppendLine(Markdown.EscapeInlineText(TB("Appendix: the conversations"))).AppendLine();
        foreach (var step in BflStep.All)
        {
            if (!input.Conversations.TryGetValue(step, out var messages) || messages.Count is 0)
                continue;

            text.Append("### ").AppendLine(Markdown.EscapeInlineText(step.Title)).AppendLine();
            if (!input.IncludePersonalDisclosures && MayHoldPersonalDisclosures(step))
            {
                text.Append('_').Append(Markdown.EscapeInlineText(TB("Left out, as it may contain personal disclosures."))).AppendLine("_").AppendLine();
                continue;
            }

            foreach (var message in messages)
            {
                var speaker = message.IsFromPerson ? TA("You") : TB("Lab");
                text.Append("**").Append(Markdown.EscapeInlineText(speaker)).AppendLine(":**").AppendLine().AppendLine(message.Text).AppendLine();
            }
        }
    }

    /// <summary>
    /// Gets the name of a kind of material.
    /// </summary>
    /// <param name="kind">The kind of material.</param>
    private static string KindName(BflMaterialKind kind) => kind switch
    {
        BflMaterialKind.DOCUMENT => TB("Document"),
        BflMaterialKind.RECORDING => TB("Recording"),
        BflMaterialKind.IMAGE => TB("Image"),
        _ => TB("Material")
    };
}