using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelApp : SettingsPanelBase
{
    private IEnumerable<ConfigurationSelectData<string>> GetFilteredTranscriptionProviders()
    {
        yield return new(T("Disable dictation and transcription"), string.Empty);

        var minimumLevel = this.SettingsManager.GetMinimumConfidenceLevel(Tools.Components.APP_SETTINGS);
        foreach (var provider in this.SettingsManager.ConfigurationData.TranscriptionProviders)
        {
            if (provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level >= minimumLevel)
                yield return new(provider.Name, provider.Id);
        }
    }

    private void UpdatePreviewFeatures(PreviewVisibility previewVisibility)
    {
        this.SettingsManager.ConfigurationData.App.PreviewVisibility = previewVisibility;
        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures = previewVisibility.FilterPreviewFeatures(this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures);
    }

    private async Task UpdateLangBehaviour(LangBehavior behavior)
    {
        this.SettingsManager.ConfigurationData.App.LanguageBehavior = behavior;
        await this.MessageBus.SendMessage<bool>(this, Event.PLUGINS_RELOADED);
    }

    private async Task UpdateManuallySelectedLanguage(Guid pluginId)
    {
        this.SettingsManager.ConfigurationData.App.LanguagePluginId = pluginId;
        await this.MessageBus.SendMessage<bool>(this, Event.PLUGINS_RELOADED);
    }
}