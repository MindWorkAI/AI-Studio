using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

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

    [Inject]
    private ChatPageSessionService ChatPageSessionService { get; init; } = null!;
    
    private ChatThread? chatThread;
    private AIStudio.Settings.Provider providerSettings = AIStudio.Settings.Provider.NONE;
    private bool workspaceOverlayVisible;
    private bool workspaceSearchVisible;
    private string currentWorkspaceName = string.Empty;
    private Workspaces? workspaces;
    private double splitterPosition = 30;
    private readonly ChatComposerState composerState = new();
    
    private readonly Timer splitterSaveTimer = new(TimeSpan.FromSeconds(1.6));

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.WORKSPACE_TOGGLE_OVERLAY ]);
        
        this.splitterPosition = this.SettingsManager.ConfigurationData.Workspace.SplitterPosition;
        this.RestoreLayoutSnapshotIfAvailable();
        this.splitterSaveTimer.AutoReset = false;
        this.splitterSaveTimer.Elapsed += async (_, _) =>
        {
            this.SettingsManager.ConfigurationData.Workspace.SplitterPosition = this.splitterPosition;
            await this.SettingsManager.StoreSettings();
        };
        
        await base.OnInitializedAsync();
        this.CheckpointLayoutState();
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
        this.CheckpointLayoutState();
    }

    private void SplitterChanged(double position)
    {
        this.splitterPosition = position;
        this.splitterSaveTimer.Stop();
        this.splitterSaveTimer.Start();
        this.CheckpointLayoutState();
    }
    
    private void ToggleWorkspacesOverlay()
    {
        this.workspaceOverlayVisible = !this.workspaceOverlayVisible;
        this.CheckpointLayoutState();
        this.StateHasChanged();
    }
    
    private double ReadSplitterPosition => this.AreWorkspacesHidden ? 6 : this.splitterPosition;
    
    private void UpdateWorkspaceName(string workspaceName)
    {
        this.currentWorkspaceName = workspaceName;
        this.CheckpointLayoutState();
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
        this.CheckpointLayoutState();
    }

    private void RestoreLayoutSnapshotIfAvailable()
    {
        var snapshot = this.ChatPageSessionService.GetLayoutSnapshot();
        if (snapshot is null)
            return;

        this.workspaceOverlayVisible = snapshot.WorkspaceOverlayVisible;
        this.workspaceSearchVisible = snapshot.WorkspaceSearchVisible;
        this.currentWorkspaceName = snapshot.CurrentWorkspaceName;
        this.splitterPosition = snapshot.SplitterPosition;
    }

    private void CheckpointLayoutState()
    {
        this.ChatPageSessionService.StoreLayoutSnapshot(new ChatPageLayoutSnapshot(
            this.workspaceOverlayVisible,
            this.workspaceSearchVisible,
            this.currentWorkspaceName,
            this.splitterPosition));
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
