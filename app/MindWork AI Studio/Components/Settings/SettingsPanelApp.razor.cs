using AIStudio.Settings.DataModel;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelApp : SettingsPanelBase
{
    private void UpdatePreviewFeatures(PreviewVisibility previewVisibility)
    {
        this.SettingsManager.ConfigurationData.App.PreviewVisibility = previewVisibility;
        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures = previewVisibility.FilterPreviewFeatures(this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures);
    }
}