using AIStudio.Dialogs;
using AIStudio.Tools.Media;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Lets users remove a plugin they installed or placed themselves.
/// </summary>
/// <remarks>
/// Without this action, such a plugin could only be removed from the data directory by hand. That is
/// especially painful for configuration plugins, which have no activation switch at all. Plugins
/// shipped with AI Studio and plugins deployed by an organization stay untouched: the action does
/// not appear for them.
/// </remarks>
public partial class PluginDeleteAction : MSGComponentBase
{
    [Parameter, EditorRequired]
    public IAvailablePlugin Plugin { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private PluginInstallService PluginInstallService { get; init; } = null!;

    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    [Inject]
    private ILogger<PluginDeleteAction> Logger { get; init; } = null!;

    private bool isDeleting;

    private bool IsAssistant => this.Plugin.Type is PluginType.ASSISTANT;

    private bool CanDelete => PluginInstallService.CanDeletePlugin(this.Plugin);

    /// <summary>
    /// True while an assistant still owns background work. We keep the action visible and block it
    /// instead of hiding it, so that the tooltip can explain why it does nothing right now.
    /// </summary>
    private bool IsBlockedByActiveWork => this.IsAssistant && this.PluginInstallService.HasActiveAssistantWork(this.Plugin.Id);

    private string Tooltip
    {
        get
        {
            if (this.IsBlockedByActiveWork)
                return this.T("The assistant cannot be deleted while background work is still running.");

            return this.Plugin.Type switch
            {
                PluginType.ASSISTANT => this.T("Delete assistant plugin"),
                PluginType.CONFIGURATION => this.T("Delete configuration plugin"),

                _ => this.T("Delete language plugin"),
            };
        }
    }

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Only an assistant can be busy. We watch its sessions and transcriptions, so the action
        // reflects the current state without the user reloading the page:
        this.ApplyFilters([], this.IsAssistant ? [Event.ASSISTANT_SESSION_CHANGED, Event.ASSISTANT_SESSION_FINISHED] : []);
        if (this.IsAssistant)
            this.MediaTranscriptionService.StateChanged += this.OnMediaTranscriptionStateChanged;

        await base.OnInitializedAsync();
    }

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.ASSISTANT_SESSION_CHANGED or Event.ASSISTANT_SESSION_FINISHED)
            this.StateHasChanged();

        return base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    protected override void DisposeResources()
    {
        if (this.IsAssistant)
            this.MediaTranscriptionService.StateChanged -= this.OnMediaTranscriptionStateChanged;

        base.DisposeResources();
    }

    #endregion

    private async Task DeletePluginAsync()
    {
        if (!this.CanDelete || this.isDeleting || this.IsBlockedByActiveWork)
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
    /// because removing it also removes the providers and settings it brought. Assistants and
    /// language plugins only own their own files, so a plain confirmation is enough.
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

        var title = this.IsAssistant
            ? this.T("Delete Assistant Plugin")
            : this.T("Delete Language Plugin");

        var message = this.IsAssistant
            ? string.Format(this.T("Do you really want to delete the assistant plugin '{0}'? This will permanently delete the local plugin files."), this.Plugin.Name)
            : string.Format(this.T("Do you really want to delete the language plugin '{0}'? This permanently deletes its local plugin files. When it is your chosen language, AI Studio returns to choosing the language automatically."), this.Plugin.Name);

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, message },
        };

        var dialog = await this.DialogService.ShowAsync<ConfirmDialog>(title, parameters, DialogOptions.FULLSCREEN);
        return await dialog.Result is { Canceled: false };
    }

    private void OnMediaTranscriptionStateChanged(MediaImportOwner owner)
    {
        if (owner.Kind is MediaImportOwnerKind.ASSISTANT && owner.Id.EndsWith($":{this.Plugin.Id}", StringComparison.Ordinal))
            _ = this.InvokeAsync(this.StateHasChanged);
    }
}