using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Security;

public sealed class PromptInjectionGuardService(
    PromptInjectionScanner scanner,
    SettingsManager settingsManager,
    ILogger<PromptInjectionGuardService> logger)
{
    public const string WIKI_URL = "https://en.wikipedia.org/wiki/Prompt_engineering#Prompt_injection";

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PromptInjectionGuardService).Namespace, nameof(PromptInjectionGuardService));

    public async Task<string> EnsureSafeForLlmAsync(string text, PromptInjectionSource source)
    {
        if (!settingsManager.ConfigurationData.Chat.EnablePromptInjectionProtection || string.IsNullOrWhiteSpace(text))
            return text;

        var result = scanner.Scan(text, source);
        if (!result.IsBlocked)
            return text;

        var message = string.Format(TB("AI Studio blocked content from '{0}' because it looks like a prompt-injection attempt."), source.Label);
        await this.HandleDetectionAsync(result, message);
        throw new PromptInjectionBlockedException(result, message);
    }

    private async Task HandleDetectionAsync(PromptInjectionScanResult result, string message)
    {
        logger.LogWarning(
            "Blocked suspected prompt injection in {SourceKind} '{SourceLabel}'. RuleIds={RuleIds}",
            result.Source.Kind,
            result.Source.Label,
            string.Join(", ", result.RuleIds));

        await MessageBus.INSTANCE.SendWarning(new(
            Icons.Material.Filled.GppMaybe,
            message));

        if (settingsManager.ConfigurationData.Chat.ShowPromptInjectionAlert)
            await MessageBus.INSTANCE.SendMessage<PromptInjectionAlertMessage>(null, Event.SHOW_PROMPT_INJECTION_ALERT, new(result));
    }
}