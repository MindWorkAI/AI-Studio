using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

using Timer = System.Timers.Timer;

namespace AIStudio.Pages;

/// <summary>
/// The chat page.
/// </summary>
public partial class Chat : MSGComponentBase
{
    private const Placement TOOLBAR_TOOLTIP_PLACEMENT = Placement.Bottom;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    private ChatThread? chatThread;
    private AIStudio.Settings.Provider providerSettings = AIStudio.Settings.Provider.NONE;
    private bool workspaceOverlayVisible;
    private bool workspaceSearchVisible;
    private string currentWorkspaceName = string.Empty;
    private Workspaces? workspaces;
    private double splitterPosition = 30;
    private bool skipRenderAfterSplitterChange;
    private readonly ChatComposerState composerState = new();
    
    private readonly Timer splitterSaveTimer = new(TimeSpan.FromSeconds(1.6));

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.WORKSPACE_TOGGLE_OVERLAY ]);
        
        this.splitterPosition = this.SettingsManager.ConfigurationData.Workspace.SplitterPosition;
        this.splitterSaveTimer.AutoReset = false;
        //
        // Mind that this handler deliberately stays off the renderer thread, although it writes the
        // configuration data from a thread pool thread. The position is a single double, and every
        // target we ship is 64 bit, so the write cannot tear -- and the worst a lost one could do is
        // a splitter standing somewhere else after the next start. What a jump to the dispatcher
        // would cost instead is paid by the user: storing the settings serializes all of them and
        // writes two files, and it would do that in the very queue which draws the drag they are in
        // the middle of. The splitter then stutters under their hand. Whoever synchronizes the
        // configuration data one day should do it without moving that work onto the renderer.
        //
        this.splitterSaveTimer.Elapsed += (_, _) =>
        {
            this.SettingsManager.ConfigurationData.Workspace.SplitterPosition = this.splitterPosition;
            this.SettingsManager.StoreSettings().Observe($"{nameof(Chat)}: storing the splitter position");
        };
        
        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Decides whether this page renders again.
    /// </summary>
    /// <remarks>
    /// Dragging the splitter reports every movement, and Blazor renders this page after each of
    /// them. That render is pure waste: all it would contribute is the position the splitter just
    /// reported, and the splitter has it already -- it renders itself after its own event, which is
    /// what resizes the two panels. What this page rebuilds instead is everything else it holds,
    /// the workspace tree above all, which has no render guard of its own and draws an item with
    /// three buttons for every chat. That is what the user sees stutter while they drag.<br/><br/>
    /// Dropping that one render costs nothing, because the splitter never needed it. Should a
    /// message from the bus ask for a render in the very same moment, this swallows it -- both sit
    /// on the same dispatcher and the render of a movement follows it without a gap, so the window
    /// is as good as closed, and the next render brings the message along anyway.
    /// </remarks>
    protected override bool ShouldRender()
    {
        if (!this.skipRenderAfterSplitterChange)
            return true;

        this.skipRenderAfterSplitterChange = false;
        return false;
    }

    #endregion
    
    private string WorkspaceSidebarToggleIcon => this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible ? Icons.Material.Filled.ArrowCircleLeft : Icons.Material.Filled.ArrowCircleRight;

    private string WorkspaceSearchIcon => this.workspaceSearchVisible ? Icons.Material.Filled.SearchOff : Icons.Material.Filled.Search;

    private string WorkspaceSearchTooltip => this.workspaceSearchVisible ? T("Hide search") : T("Search your workspaces");

    private bool AreWorkspacesVisible => this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is not WorkspaceStorageBehavior.DISABLE_WORKSPACES
                                         && ((this.SettingsManager.ConfigurationData.Workspace.DisplayBehavior is WorkspaceDisplayBehavior.TOGGLE_SIDEBAR && this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible)
                                         || this.SettingsManager.ConfigurationData.Workspace.DisplayBehavior is WorkspaceDisplayBehavior.SIDEBAR_ALWAYS_VISIBLE);

    private bool AreWorkspacesHidden => this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is not WorkspaceStorageBehavior.DISABLE_WORKSPACES
                                        && this.SettingsManager.ConfigurationData.Workspace.DisplayBehavior is WorkspaceDisplayBehavior.TOGGLE_SIDEBAR
                                        && !this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible;

    private async Task ToggleWorkspaceSidebar()
    {
        this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible = !this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible;
        await this.SettingsManager.StoreSettings();
    }

    private void SplitterChanged(double position)
    {
        this.splitterPosition = position;
        this.splitterSaveTimer.Stop();
        this.splitterSaveTimer.Start();
        this.skipRenderAfterSplitterChange = true;
    }
    
    private void ToggleWorkspacesOverlay()
    {
        this.workspaceOverlayVisible = !this.workspaceOverlayVisible;
        this.StateHasChanged();
    }
    
    private double ReadSplitterPosition => this.AreWorkspacesHidden ? 6 : this.splitterPosition;
    
    private void UpdateWorkspaceName(string workspaceName)
    {
        this.currentWorkspaceName = workspaceName;
        this.StateHasChanged();
    }

    private async Task OpenChatSettingsDialog()
    {
        var dialogParameters = new DialogParameters();
        
        await this.DialogService.ShowAsync<SettingsDialogChat>(T("Open Chat Options"), dialogParameters, DialogOptions.FULLSCREEN);
    }
    
    private async Task OpenWorkspacesSettingsDialog()
    {
        var dialogParameters = new DialogParameters();
        await this.DialogService.ShowAsync<SettingsDialogWorkspaces>(T("Open Workspaces Configuration"), dialogParameters, DialogOptions.FULLSCREEN);
    }

    private async Task RefreshWorkspaces()
    {
        if (this.workspaces is null)
            return;

        await this.workspaces.ForceRefreshFromDiskAsync();
    }

    private async Task ToggleWorkspaceSearch()
    {
        if (this.workspaces is null)
            return;

        await this.workspaces.ToggleSearchAsync();
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        try
        {
            this.splitterSaveTimer.Stop();
            this.splitterSaveTimer.Dispose();
        }
        catch
        {
            // ignore
        }
        
        base.DisposeResources();
    }

    #endregion
    
    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.WORKSPACE_TOGGLE_OVERLAY:
                this.ToggleWorkspacesOverlay();
                break;
        }

        return Task.CompletedTask;
    }
}