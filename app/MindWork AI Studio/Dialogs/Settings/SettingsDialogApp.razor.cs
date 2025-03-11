using AIStudio.Settings.DataModel;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogApp : SettingsDialogBase
{
    private void UpdatePreviewFeatures(PreviewVisibility previewVisibility)
    {
        this.SettingsManager.ConfigurationData.App.PreviewVisibility = previewVisibility;
        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures = previewVisibility.FilterPreviewFeatures(this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures);
    }

}