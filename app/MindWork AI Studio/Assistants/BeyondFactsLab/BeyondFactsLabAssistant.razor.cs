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

    /// <summary>
    /// Whether the person may open the lab, so that the page did not send them away.
    /// </summary>
    private bool mayOpen;

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
        this.mayOpen = true;
        await this.RunStoreActionAsync(() => this.ReloadProjectsAsync(null));
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender && this.mayOpen)
            await this.ShowNoticeIfNeededAsync();
    }

    /// <summary>
    /// Shows the notes of the lab, unless the person confirmed them as they are now.
    /// </summary>
    private async Task ShowNoticeIfNeededAsync()
    {
        var currentHash = await BflNotice.ComputeHashAsync();
        if (BflNotice.IsAccepted(this.SettingsManager.ConfigurationData.BeyondFactsLab.AcceptedNoticeHash, currentHash))
            return;

        var languagePlugin = await this.SettingsManager.GetActiveLanguagePlugin();
        var notice = await BflNotice.ReadAsync(languagePlugin.IETFTag);
        if (string.IsNullOrWhiteSpace(notice))
        {
            this.Logger.LogError("The notes of the BeyondFacts Lab are missing.");
            return;
        }

        var parameters = new DialogParameters<BflNoticeDialog> { { dialog => dialog.Notice, notice } };
        var reference = await this.DialogService.ShowAsync<BflNoticeDialog>(T("BeyondFacts Lab"), parameters, DialogOptions.BLOCKING_FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        this.SettingsManager.ConfigurationData.BeyondFactsLab.AcceptedNoticeHash = currentHash;
        await this.SettingsManager.StoreSettings();
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