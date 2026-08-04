using AIStudio.Dialogs;
using AIStudio.Tools.Media;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class AssistantPluginDeleteAction : MSGComponentBase
{
    [Parameter, EditorRequired]
    public IAvailablePlugin Plugin { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private AssistantPluginInstallService AssistantPluginInstallService { get; init; } = null!;

    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    [Inject]
    private ILogger<AssistantPluginDeleteAction> Logger { get; init; } = null!;

    private bool CanDelete => AssistantPluginInstallService.CanDeleteInstalledAssistant(this.Plugin);

    private bool IsBlockedByActiveWork => this.AssistantPluginInstallService.HasActiveAssistantWork(this.Plugin.Id);

    private string Tooltip => this.IsBlockedByActiveWork
        ? this.T("The assistant cannot be deleted while background work is still running.")
        : this.T("Delete assistant plugin");

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.ASSISTANT_SESSION_CHANGED, Event.ASSISTANT_SESSION_FINISHED ]);
        this.MediaTranscriptionService.StateChanged += this.OnMediaTranscriptionStateChanged;
        await base.OnInitializedAsync();
    }

    private async Task DeleteAssistantPluginAsync()
    {
        if (!this.CanDelete || this.IsBlockedByActiveWork)
            return;

        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            {
                x => x.Message,
                string.Format(this.T("Do you really want to delete the assistant plugin '{0}'? This will permanently delete the local plugin files."), this.Plugin.Name)
            },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(this.T("Delete Assistant Plugin"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var result = await this.AssistantPluginInstallService.DeleteInstalledAssistantAsync(this.Plugin, CancellationToken.None);
        if (!result.Success)
        {
            this.Logger.LogError("Failed to delete assistant plugin '{PluginName}' ({PluginId}) from '{PluginDirectory}' with issue '{Issue}'.", result.PluginName, result.PluginId, result.PluginDirectory, result.Issue);
            await this.MessageBus.SendError(new(Icons.Material.Filled.DeleteForever, string.Format(this.T("The assistant plugin '{0}' could not be deleted: {1}"), this.Plugin.Name, result.Issue)));
            return;
        }

        await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Check, string.Format(this.T("The '{0}' assistant plugin has been successfully removed."), result.PluginName)));
    }

    private void OnMediaTranscriptionStateChanged(MediaImportOwner owner)
    {
        if (owner.Kind is MediaImportOwnerKind.ASSISTANT && owner.Id.EndsWith($":{this.Plugin.Id}", StringComparison.Ordinal))
            _ = this.InvokeAsync(this.StateHasChanged);
    }

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.ASSISTANT_SESSION_CHANGED or Event.ASSISTANT_SESSION_FINISHED)
            this.StateHasChanged();

        return base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    protected override void DisposeResources()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnMediaTranscriptionStateChanged;
        base.DisposeResources();
    }
}
