using AIStudio.Dialogs.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// The title of a dialog about compressing a chat, with a gear which opens the settings of chat compression.
/// </summary>
/// <remarks>
/// Used by the dialog of compressing and by the notice before sending, see ChatCompressionDialog and
/// ContextWindowFullDialog. Both decide anew once the settings close, since the user may have changed what they
/// show, such as the model which writes the summary.
/// </remarks>
public partial class DialogTitleWithChatCompressionSettings : MSGComponentBase
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    /// <summary>
    /// The title of the dialog.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Whether the gear is switched off, e.g., while the AI writes the summary.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Called once the settings are closed again.
    /// </summary>
    [Parameter]
    public EventCallback SettingsClosed { get; set; }

    private async Task OpenSettingsAsync()
    {
        var dialogReference = await this.DialogService.ShowAsync<SettingsDialogChatCompression>(null, new DialogParameters(), DialogOptions.FULLSCREEN);
        await dialogReference.Result;
        await this.SettingsClosed.InvokeAsync();
    }
}