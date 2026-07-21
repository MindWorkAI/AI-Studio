using System.Net;

using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

/// <summary>
/// Adds a disclosure to AI-generated text before it leaves AI Studio.
/// </summary>
public sealed class AIGeneratedContentDisclosureService(SettingsManager settingsManager, RustService rustService)
{
    private const string DISCLOSURE_DE = "Dieser Inhalt wurde mit künstlicher Intelligenz (KI) erstellt.";
    private const string DISCLOSURE_EN = "This content was generated using artificial intelligence (AI).";

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AIGeneratedContentDisclosureService).Namespace, nameof(AIGeneratedContentDisclosureService));

    private SettingsManager SettingsManager { get; } = settingsManager;

    private RustService RustService { get; } = rustService;

    /// <summary>
    /// Adds the disclosure to non-empty text when the feature is enabled.
    /// </summary>
    public async Task<string> AddDisclosureToMarkdown(string text)
    {
        if (!this.SettingsManager.ConfigurationData.App.AddAIGeneratedContentDisclosure || string.IsNullOrWhiteSpace(text))
            return text;

        var contentWithoutTrailingLineBreaks = text.TrimEnd('\r', '\n');
        var disclosure = AIStudio.Tools.Markdown.EscapeInlineText(await this.GetDisclosure());
        return $"{contentWithoutTrailingLineBreaks}{Environment.NewLine}{Environment.NewLine}**{disclosure}**";
    }

    /// <summary>
    /// Copies AI-generated text with the configured disclosure.
    /// </summary>
    public async Task CopyText2Clipboard(ISnackbar snackbar, string text)
    {
        if (!this.SettingsManager.ConfigurationData.App.AddAIGeneratedContentDisclosure || string.IsNullOrWhiteSpace(text))
        {
            await this.RustService.CopyText2Clipboard(snackbar, text);
            return;
        }

        var contentWithoutTrailingLineBreaks = text.TrimEnd('\r', '\n');
        var disclosure = await this.GetDisclosure();
        var plainText = $"{contentWithoutTrailingLineBreaks}{Environment.NewLine}{Environment.NewLine}{disclosure}";
        var htmlText = $"<div style=\"white-space: pre-wrap;\">{WebUtility.HtmlEncode(contentWithoutTrailingLineBreaks)}</div><div style=\"font-weight: bold; margin-top: 1em;\">{WebUtility.HtmlEncode(disclosure)}</div>";
        await this.RustService.CopyRichText2Clipboard(snackbar, plainText, htmlText);
    }

    private async Task<string> GetDisclosure()
    {
        var translatedDisclosure = TB("This content was generated using artificial intelligence (AI).");
        if (!string.Equals(translatedDisclosure, DISCLOSURE_EN, StringComparison.Ordinal))
            return translatedDisclosure;

        var language = await this.SettingsManager.GetActiveLanguagePlugin();
        return language.IETFTag.StartsWith("de-", StringComparison.OrdinalIgnoreCase)
            ? DISCLOSURE_DE
            : translatedDisclosure;
    }
}