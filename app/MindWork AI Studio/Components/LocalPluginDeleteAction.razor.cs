using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Lets users remove a configuration or language plugin they installed or placed themselves.
/// </summary>
/// <remarks>
/// Without this action, such a plugin could only be removed from the data directory by hand. That is
/// especially painful for configuration plugins, which have no activation switch at all. Plugins
/// shipped with AI Studio and plugins deployed by an organization stay untouched: the action does
/// not appear for them.
/// </remarks>
public partial class LocalPluginDeleteAction : MSGComponentBase
{
    [Parameter, EditorRequired]
    public IAvailablePlugin Plugin { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private PluginInstallService PluginInstallService { get; init; } = null!;

    [Inject]
    private ILogger<LocalPluginDeleteAction> Logger { get; init; } = null!;

    private bool isDeleting;

    // The type check keeps this action apart from the AssistantPluginDeleteAction next to it, which
    // covers assistants. Both are merged into one component in a follow-up:
    private bool CanDelete => this.Plugin.Type is not PluginType.ASSISTANT && PluginInstallService.CanDeletePlugin(this.Plugin);

    private string Tooltip => this.Plugin.Type is PluginType.CONFIGURATION
        ? this.T("Delete configuration plugin")
        : this.T("Delete language plugin");

    private async Task DeleteLocalPluginAsync()
    {
        if (!this.CanDelete || this.isDeleting)
            return;

        if (!await this.ConfirmDeletionAsync())
            return;

        this.isDeleting = true;
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            var result = await this.PluginInstallService.DeletePluginAsync(this.Plugin, CancellationToken.None);
            if (!result.Success)
            {
                this.Logger.LogError("Failed to delete {PluginType} plugin '{PluginName}' ({PluginId}) from '{PluginDirectory}' with issue '{Issue}'.", this.Plugin.Type, result.PluginName, result.PluginId, result.PluginDirectory, result.Issue);
                await this.MessageBus.SendError(new(Icons.Material.Filled.DeleteForever, string.Format(this.T("The plugin '{0}' could not be deleted: {1}"), this.Plugin.Name, result.Issue)));
                return;
            }

            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Check, string.Format(this.T("The plugin '{0}' has been successfully removed."), result.PluginName)));
        }
        finally
        {
            this.isDeleting = false;
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    /// <summary>
    /// Asks the user before the deletion. A configuration gets the dialog listing its consequences,
    /// because removing it also removes the providers and settings it brought. A language plugin
    /// only owns its own files, so a plain confirmation is enough.
    /// </summary>
    private async Task<bool> ConfirmDeletionAsync()
    {
        if (this.Plugin.Type is PluginType.CONFIGURATION)
        {
            var configurationParameters = new DialogParameters<ConfigurationPluginDeleteDialog>
            {
                { x => x.PluginName, this.Plugin.Name },
                { x => x.Summary, this.PluginInstallService.BuildConfigurationDeleteSummary(this.Plugin) },
            };

            var configurationDialog = await this.DialogService.ShowAsync<ConfigurationPluginDeleteDialog>(this.T("Delete Configuration Plugin"), configurationParameters, DialogOptions.FULLSCREEN);
            return await configurationDialog.Result is { Canceled: false };
        }

        var parameters = new DialogParameters<ConfirmDialog>
        {
            {
                x => x.Message,
                string.Format(this.T("Do you really want to delete the language plugin '{0}'? This permanently deletes its local plugin files. When it is your chosen language, AI Studio returns to choosing the language automatically."), this.Plugin.Name)
            },
        };

        var dialog = await this.DialogService.ShowAsync<ConfirmDialog>(this.T("Delete Language Plugin"), parameters, DialogOptions.FULLSCREEN);
        return await dialog.Result is { Canceled: false };
    }
}