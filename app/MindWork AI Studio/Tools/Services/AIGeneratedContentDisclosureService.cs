using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

/// <summary>
/// Adds a notice to AI-generated content before it leaves AI Studio, which says that AI generated it.
/// </summary>
/// <remarks>
/// The notice is off by default. Whether a text has to say that AI generated it depends on what happens
/// to it afterward, for example, whether it is published, as transparency rules such as the EU AI Act
/// ask. Only the user or their organization knows that, so the user switches it on in the app settings,
/// or an organization does so for everybody with a configuration plugin.
///
/// The notice ends the content as a paragraph of its own, in the language of the app. It leaves the
/// answer itself untouched, and a reader finds it where the text ends, below the sources as well.
/// It is Markdown, like everything else AI Studio copies or exports, which is why it is bold in a
/// document and stays readable as plain text.
///
/// Only content a reader takes as a whole gets the notice: an answer which is copied or exported, and
/// the files of a batch run. Where one sentence more breaks what the user wanted, it stays away: a code
/// block, a single file or table out of an answer, the table of a batch run, and the Lua code of the
/// assistant builder. The prompt of the prompt optimizer goes on to another model instead of a reader,
/// and a visual briefing is exported as the very version AI Studio checked, byte for byte, so neither
/// of them gets the notice. Messages of the user, transcripts, IDs, and paths are no AI answers.
/// </remarks>
public sealed class AIGeneratedContentDisclosureService(SettingsManager settingsManager)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AIGeneratedContentDisclosureService).Namespace, nameof(AIGeneratedContentDisclosureService));

    /// <summary>
    /// Adds the notice below the given Markdown text, when it is switched on.
    /// </summary>
    /// <param name="markdown">The Markdown text which AI generated.</param>
    /// <returns>The text followed by the notice, or the text itself when the notice is off or there is no text.</returns>
    public string AddDisclosureToMarkdown(string markdown)
    {
        if (!settingsManager.ConfigurationData.App.AddAIGeneratedContentDisclosure || string.IsNullOrWhiteSpace(markdown))
            return markdown;

        var disclosure = Markdown.EscapeInlineText(TB("This content was generated using artificial intelligence (AI)."));
        return Markdown.AppendBlock(markdown.TrimEnd('\r', '\n'), $"**{disclosure}**");
    }
}