using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogFormatTemplates : SettingsDialogBase
{
    /// <summary>
    /// Opens the management of the format templates, as the places which offer the templates do,
    /// e.g., the export menu of an answer and the Slide Assistant.
    /// </summary>
    /// <param name="service">The dialog service.</param>
    /// <returns>A task which completes once the user closed the dialog.</returns>
    public static async Task ShowAsync(IDialogService service)
    {
        var dialogReference = await service.ShowAsync<SettingsDialogFormatTemplates>(null, new DialogParameters(), DialogOptions.FULLSCREEN);
        await dialogReference.Result;
    }
}