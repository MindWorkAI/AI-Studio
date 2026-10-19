namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogTranslation : SettingsDialogBase
{
    private async Task OpenGlossarySettings()
    {
        var dialogParameters = new DialogParameters();
        await this.DialogService.ShowAsync<SettingsDialogGlossaries>(T("Open Glossary Options"), dialogParameters, DialogOptions.FULLSCREEN);
    }
}