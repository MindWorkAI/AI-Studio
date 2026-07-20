using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelConfidence : SettingsPanelBase
{
    private string GetCurrentConfidenceLevelName(LLMProviders llmProvider)
    {
        if (this.SettingsManager.ConfigurationData.Confidence.CustomConfidenceScheme.TryGetValue(llmProvider, out var level))
            return level.GetName();

        return T("Not yet configured");
    }
    
    private string SetCurrentConfidenceLevelColorStyle(LLMProviders llmProvider)
    {
        if (this.SettingsManager.ConfigurationData.Confidence.CustomConfidenceScheme.TryGetValue(llmProvider, out var level))
            return $"background-color: {level.GetColor(this.SettingsManager)};";

        return $"background-color: {ConfidenceLevel.UNKNOWN.GetColor(this.SettingsManager)};";
    }

    private bool IsCustomConfidenceSchemeLocked()
    {
        return ManagedConfiguration.TryGet(x => x.Confidence, x => x.CustomConfidenceScheme, out var meta) && meta.IsLocked;
    }

    private async Task ChangeCustomConfidenceLevel(LLMProviders llmProvider, ConfidenceLevel level)
    {
        if (this.IsCustomConfidenceSchemeLocked())
            return;
        
        this.SettingsManager.ConfigurationData.Confidence.CustomConfidenceScheme[llmProvider] = level;
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}