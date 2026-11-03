namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogAssistantBuilder : SettingsDialogBase
{
    private bool DefaultsDisabled() => !this.SettingsManager.ConfigurationData.AssistantBuilder.PreselectOptions;
}