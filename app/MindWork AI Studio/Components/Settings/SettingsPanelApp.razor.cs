using AIStudio.Settings.DataModel;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelApp : SettingsPanelBase
{
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