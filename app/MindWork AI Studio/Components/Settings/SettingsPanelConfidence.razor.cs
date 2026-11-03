using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelConfidence : SettingsPanelBase
{
    private string GetCurrentConfidenceLevelName(LLMProviders llmProvider)
    {
        if (this.TryGetShownConfidenceLevel(llmProvider, out var level))
            return level.GetName();

        return T("Not yet configured");
    }

    private string SetCurrentConfidenceLevelColorStyle(LLMProviders llmProvider)
    {
        if (this.TryGetShownConfidenceLevel(llmProvider, out var level))
            return $"background-color: {level.GetColor(this.SettingsManager)};";

        return $"background-color: {ConfidenceLevel.UNKNOWN.GetColor(this.SettingsManager)};";
    }

    /// <summary>
    /// Determines the confidence level the table shows for a provider.
    /// </summary>
    /// <remarks>
    /// A prohibited provider is untrusted, whatever level was chosen for it, see
    /// SettingsManager.GetConfiguredConfidenceLevel. The chosen level stays stored, so the table
    /// shows it again once the prohibition ends.
    /// </remarks>
    private bool TryGetShownConfidenceLevel(LLMProviders llmProvider, out ConfidenceLevel level)
    {
        if (this.SettingsManager.IsProviderProhibited(llmProvider))
        {
            level = ConfidenceLevel.UNTRUSTED;
            return true;
        }

        return this.SettingsManager.ConfigurationData.Confidence.CustomConfidenceScheme.TryGetValue(llmProvider, out level);
    }

    private bool IsCustomConfidenceSchemeLocked()
    {
        return ManagedConfiguration.TryGet(x => x.Confidence, x => x.CustomConfidenceScheme, out var meta) && meta.IsLocked;
    }

    private bool IsConfidenceLevelLocked(LLMProviders llmProvider) => this.IsCustomConfidenceSchemeLocked() || this.SettingsManager.IsProviderProhibited(llmProvider);

    private async Task ChangeCustomConfidenceLevel(LLMProviders llmProvider, ConfidenceLevel level)
    {
        if (this.IsConfidenceLevelLocked(llmProvider))
            return;

        this.SettingsManager.ConfigurationData.Confidence.CustomConfidenceScheme[llmProvider] = level;
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}