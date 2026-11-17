using AIStudio.Dialogs.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// The content of the notification that the user hid a category, see DataAssistantCategoryHiddenMessage.
/// </summary>
/// <remarks>
/// The gear opens the settings of the assistants page right away and closes the notification, since the
/// dialog shows the way back itself.
/// </remarks>
public partial class AssistantCategoryHiddenMessage : MSGComponentBase
{
    /// <summary>
    /// The categories the settings dialog offers.
    /// </summary>
    [Parameter]
    public IReadOnlyList<AssistantCategoryKey> Categories { get; set; } = [];

    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    private async Task OpenSettingsAsync()
    {
        this.Snackbar.RemoveByKey(DataAssistantCategoryHiddenMessage.KEY);
        var dialogParameters = new DialogParameters<SettingsDialogAssistantsPage>
        {
            { x => x.Categories, this.Categories }
        };

        await this.DialogService.ShowAsync<SettingsDialogAssistantsPage>(T("Assistant Settings"), dialogParameters, DialogOptions.FULLSCREEN);
    }
}