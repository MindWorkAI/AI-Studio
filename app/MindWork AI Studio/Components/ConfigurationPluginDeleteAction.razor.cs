using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Lets users remove a configuration plugin they placed locally.
/// </summary>
/// <remarks>
/// Configuration plugins have no activation switch, so without this action a local configuration
/// could only be removed from the data directory by hand. Configurations deployed by an organization
/// stay untouched: the action does not appear for them.
/// </remarks>
public partial class ConfigurationPluginDeleteAction : MSGComponentBase
{
    [Parameter, EditorRequired]
    public IAvailablePlugin Plugin { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private PluginInstallService PluginInstallService { get; init; } = null!;

    [Inject]
    private ILogger<ConfigurationPluginDeleteAction> Logger { get; init; } = null!;

    private bool isDeleting;

    private bool CanDelete => PluginInstallService.CanDeleteInstalledConfiguration(this.Plugin);

    private async Task DeleteConfigurationPluginAsync()
    {
        if (!this.CanDelete || this.isDeleting)
            return;

        // The deletion reaches beyond the plugin directory, so we show what it takes with it:
        var dialogParameters = new DialogParameters<ConfigurationPluginDeleteDialog>
        {
            { x => x.PluginName, this.Plugin.Name },
            { x => x.Summary, this.PluginInstallService.BuildConfigurationDeleteSummary(this.Plugin) },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfigurationPluginDeleteDialog>(this.T("Delete Configuration Plugin"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        this.isDeleting = true;
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            var result = await this.PluginInstallService.DeleteInstalledConfigurationAsync(this.Plugin, CancellationToken.None);
            if (!result.Success)
            {
                this.Logger.LogError("Failed to delete configuration plugin '{PluginName}' ({PluginId}) from '{PluginDirectory}' with issue '{Issue}'.", result.PluginName, result.PluginId, result.PluginDirectory, result.Issue);
                await this.MessageBus.SendError(new(Icons.Material.Filled.DeleteForever, string.Format(this.T("The configuration plugin '{0}' could not be deleted: {1}"), this.Plugin.Name, result.Issue)));
                return;
            }

            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Check, string.Format(this.T("The '{0}' configuration plugin has been successfully removed."), result.PluginName)));
        }
        finally
        {
            this.isDeleting = false;
            await this.InvokeAsync(this.StateHasChanged);
        }
    }
}