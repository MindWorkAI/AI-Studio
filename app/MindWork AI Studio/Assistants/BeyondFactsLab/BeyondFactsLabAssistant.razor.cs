using AIStudio.Components;
using AIStudio.Dialogs.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The page of the BeyondFacts Lab, which guides researchers through the preparation of a challenging appearance.
/// </summary>
/// <remarks>
/// The lab keeps several preparations, each with steps of their own, so it does not inherit AssistantBase,
/// which knows one input and one result. Like the Visual Briefing assistant, it checks by itself whether it
/// may be opened.
/// </remarks>
public partial class BeyondFactsLabAssistant : MSGComponentBase
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    [Inject]
    private BeyondFactsLabStore Store { get; init; } = null!;

    [Inject]
    private ILogger<BeyondFactsLabAssistant> Logger { get; init; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (!BflAccess.CanOpen(this.SettingsManager, T("BeyondFacts Lab")))
        {
            this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
            return;
        }

        this.ApplyFilters([], [Event.CONFIGURATION_CHANGED]);
        await this.RunStoreActionAsync(() => this.ReloadProjectsAsync(null));
    }

    /// <inheritdoc />
    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        // An organization may hide the lab, or the person may switch its preview feature off, while the page is open:
        if (triggeredEvent is Event.CONFIGURATION_CHANGED && !BflAccess.CanOpen(this.SettingsManager))
        {
            this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
            return;
        }

        await base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    private async Task OpenSettingsDialogAsync() => await this.DialogService.ShowAsync<SettingsDialogBeyondFactsLab>(null, new DialogParameters(), DialogOptions.FULLSCREEN);
}