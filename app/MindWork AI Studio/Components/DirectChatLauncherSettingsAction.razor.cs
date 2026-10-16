using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem.Assistants;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Lets users change the chat a direct chat launcher opens, right from its tile.
/// </summary>
/// <remarks>
/// A launcher tile has no assistant page: opening it goes straight to the chat, so the revise
/// action on the dynamic assistant page can never be reached for one. Its tile is therefore the
/// place where users look for its settings.
/// </remarks>
public partial class DirectChatLauncherSettingsAction : MSGComponentBase
{
    [Parameter, EditorRequired]
    public PluginAssistants Plugin { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<DirectChatLauncherSettingsAction> Logger { get; init; } = null!;

    private bool isEditing;

    //
    // This check reads no files on purpose: it runs on every render of the assistants page. Whether
    // the plugin file itself can be rewritten is decided by the dialog, which reads it anyway:
    //
    private bool CanEditSettings => DirectChatLauncherLuaWriter.CanRewrite(this.Plugin);

    private async Task OpenSettingsDialogAsync()
    {
        if (!this.CanEditSettings || this.isEditing)
            return;

        this.isEditing = true;
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            var parameters = new DialogParameters<DirectChatLauncherSettingsDialog>
            {
                { x => x.PluginId, this.Plugin.Id },
                { x => x.PluginLocalPath, this.Plugin.PluginPath },
            };

            var dialogReference = await this.DialogService.ShowAsync<DirectChatLauncherSettingsDialog>(this.T("Tile Settings"), parameters, DialogOptions.BLOCKING_FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not DirectChatLauncherSettingsDialogResult result)
                return;

            this.Logger.LogInformation("The chat launcher '{PluginName}' ({PluginId}) has been updated from its tile.", result.PluginName, result.PluginId);
            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Save, string.Format(this.T("The tile '{0}' has been updated."), result.PluginName)));

            // Saving already ran LoadAll, which announced PLUGINS_RELOADED. We still announce the
            // configuration change: with automatic audits enabled, the dialog stored an audit result:
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
        }
        finally
        {
            this.isEditing = false;
            await this.InvokeAsync(this.StateHasChanged);
        }
    }
}