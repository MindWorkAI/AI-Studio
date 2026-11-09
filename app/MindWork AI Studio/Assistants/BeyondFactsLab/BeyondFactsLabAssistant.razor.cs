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
    private BflConversationService ConversationService { get; init; } = null!;

    [Inject]
    private BflMaterialService MaterialService { get; init; } = null!;

    [Inject]
    private ILogger<BeyondFactsLabAssistant> Logger { get; init; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    /// <summary>
    /// Whether the person may open the lab, so that the page did not send them away.
    /// </summary>
    private bool mayOpen;

    /// <summary>
    /// The empty element right before the header of the preparation, which marks where the header sits in the flow.
    /// </summary>
    private ElementReference stickyHeaderAnchor;

    /// <summary>
    /// The header of the preparation, which stays at the top while the person scrolls.
    /// </summary>
    private ElementReference stickyHeader;

    /// <summary>
    /// The ID of the header element whose height the page follows, so that only a new header element needs following.
    /// </summary>
    private string? followedStickyHeaderId;

    /// <summary>
    /// Whether the next render shows another step, which then starts at the top.
    /// </summary>
    private bool scrollToActiveStep;

    /// <summary>
    /// Whether the area which tells how to work with the lab is open.
    /// </summary>
    private bool introductionExpanded;

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
        this.MaterialService.MaterialsChanged += this.OnMaterialsChangedAsync;
        this.mayOpen = true;
        await this.RunStoreActionAsync(() => this.ReloadProjectsAsync(null));

        // Whoever has no preparation yet needs to know how the lab works first:
        this.introductionExpanded = this.projects.Count is 0;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (this.scrollToActiveStep)
        {
            // The person scrolled down in one step and chose another one; it starts right under the header:
            this.scrollToActiveStep = false;
            await this.stickyHeaderAnchor.ScrollToStickyHeaderAsync(this.JsRuntime);
        }

        //
        // The material sticks right below the header, so the page has to know how tall the header is. Opening a
        // preparation after none, or after one which is unavailable, renders a new header element:
        //
        if (this.stickyHeader.Id is { } headerId && headerId != this.followedStickyHeaderId)
        {
            this.followedStickyHeaderId = headerId;
            await this.stickyHeader.FollowStickyHeaderAsync(this.JsRuntime);
        }

        if (firstRender && this.mayOpen)
            await this.ShowNoticeIfNeededAsync();
    }

    /// <summary>
    /// Shows the notes of the lab, unless the person confirmed them as they are now.
    /// </summary>
    private async Task ShowNoticeIfNeededAsync()
    {
        var notice = BflNotice.Text;
        var currentHash = BflNotice.ComputeHash(notice);
        if (BflNotice.IsAccepted(this.SettingsManager.ConfigurationData.BeyondFactsLab.AcceptedNoticeHash, currentHash))
            return;

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
        if (triggeredEvent is Event.CONFIGURATION_CHANGED)
        {
            // An organization may hide the lab, or the person may switch its preview feature off, while the page is open:
            if (!BflAccess.CanOpen(this.SettingsManager))
            {
                this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
                return;
            }

            // A changed option, such as the form of address in the options of the lab, shows right away:
            this.StateHasChanged();
        }

        await base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.MaterialService.MaterialsChanged -= this.OnMaterialsChangedAsync;
        base.DisposeResources();
    }

    /// <summary>
    /// Shows a preparation as changed once its material changed: material is work on the preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation whose material changed.</param>
    private Task OnMaterialsChangedAsync(Guid projectId)
    {
        if (this.projects.All(project => project.ProjectId != projectId))
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            if (await this.Store.LoadAsync(projectId) is not { } project)
                return;

            this.ReplaceProject(project);
            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Remembers whether the person opened or closed the area which tells how to work with the lab.
    /// </summary>
    /// <param name="isExpanded">Whether the area is open now.</param>
    private Task IntroductionExpandedChanged(bool isExpanded)
    {
        this.introductionExpanded = isExpanded;
        return Task.CompletedTask;
    }

    private async Task OpenSettingsDialogAsync() => await this.DialogService.ShowAsync<SettingsDialogBeyondFactsLab>(null, new DialogParameters(), DialogOptions.FULLSCREEN);
}