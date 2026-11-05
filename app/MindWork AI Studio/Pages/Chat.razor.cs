using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

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
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    private ILogger<Chat> Logger { get; init; } = null!;

    private ChatThread? chatThread;
    private AIStudio.Settings.Provider providerSettings = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// Whether an answer is being written for the open chat, as the chat component last told.
    /// </summary>
    /// <remarks>
    /// The provider selection is locked meanwhile. Another provider would not reach the answer, which
    /// keeps the one it started with, but the chat would show the other one while the answer is still
    /// being written.
    /// </remarks>
    private bool isAnswerBeingWritten;

    private bool workspaceOverlayVisible;
    private bool workspaceSearchVisible;
    private readonly WorkspaceSearch workspaceSearch = new();
    private string currentWorkspaceName = string.Empty;
    private Workspaces? workspaces;
    private ChatComponent? chatComponent;
    private readonly ChatComposerState composerState = new();

    private ElementReference sidebarElement;
    private ElementReference sidebarHandle;

    /// <summary>
    /// Whether the script lets the user drag the handle beside the sidebar shown now.
    /// </summary>
    private bool isSidebarResizeWatched;

    private DotNetObjectReference<Chat>? dotNetReference;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.WORKSPACE_TOGGLE_OVERLAY ]);
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        //
        // The sidebar and its handle are created anew whenever the sidebar shows up again, and the
        // script then has to watch the new handle:
        //
        if (!this.AreWorkspacesVisible)
            this.isSidebarResizeWatched = false;

        else if (!this.isSidebarResizeWatched)
        {
            this.isSidebarResizeWatched = true;
            this.dotNetReference ??= DotNetObjectReference.Create(this);

            //
            // Resizing is a convenience. An exception from here would end the whole circuit, though,
            // and the page would freeze while it still loads. A browser which still runs a cached
            // app.js from an older version lacks the function:
            //
            try
            {
                await this.JsRuntime.InvokeVoidAsync("watchSidebarResize", this.sidebarHandle, this.sidebarElement, this.dotNetReference);
            }
            catch (JSDisconnectedException)
            {
                // Nobody is left to see the sidebar.
            }
            catch (JSException ex)
            {
                this.Logger.LogWarning(ex, "Failed to let the user resize the workspace sidebar.");
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion
    
    private string WorkspaceSidebarToggleIcon => this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible ? Icons.Material.Filled.ArrowCircleLeft : Icons.Material.Filled.ArrowCircleRight;

    private string WorkspaceSearchIcon => this.workspaceSearchVisible ? Icons.Material.Filled.SearchOff : Icons.Material.Filled.Search;

    private string WorkspaceSearchTooltip => this.workspaceSearchVisible ? T("Close the search") : T("Search your workspaces");

    private bool AreWorkspacesVisible => this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is not WorkspaceStorageBehavior.DISABLE_WORKSPACES
                                         && ((this.SettingsManager.ConfigurationData.Workspace.DisplayBehavior is WorkspaceDisplayBehavior.TOGGLE_SIDEBAR && this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible)
                                         || this.SettingsManager.ConfigurationData.Workspace.DisplayBehavior is WorkspaceDisplayBehavior.SIDEBAR_ALWAYS_VISIBLE);

    private async Task ToggleWorkspaceSidebar()
    {
        this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible = !this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible;
        await this.SettingsManager.StoreSettings();
    }

    /// <summary>
    /// The width the user dragged the sidebar to, which Chat.razor.css keeps within its limits.
    /// </summary>
    private string SidebarWidthStyle => FormattableString.Invariant($"--sidebar-width: {this.SettingsManager.ConfigurationData.Workspace.SidebarWidth}px;");

    /// <summary>
    /// Stores the width of the sidebar, once the user let go of the handle beside it.
    /// </summary>
    /// <remarks>
    /// The script resized the sidebar while the user dragged, without a round trip to the server for
    /// every movement, so the page needs no render for it.
    /// </remarks>
    /// <param name="width">The width the sidebar took, in CSS pixels.</param>
    [JSInvokable]
    public async Task SidebarResizedAsync(double width)
    {
        this.SettingsManager.ConfigurationData.Workspace.SidebarWidth = width;
        await this.SettingsManager.StoreSettings();
    }

    private void ToggleWorkspacesOverlay()
    {
        this.workspaceOverlayVisible = !this.workspaceOverlayVisible;
        this.StateHasChanged();
    }

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

    private void ToggleWorkspaceSearch()
    {
        if (this.workspaceSearchVisible)
            this.CloseWorkspaceSearch();
        else
            this.workspaceSearchVisible = true;
    }

    /// <summary>
    /// Closes the search of the workspaces, after which the list shows all workspaces and chats again.
    /// </summary>
    private void CloseWorkspaceSearch()
    {
        this.workspaceSearchVisible = false;
        this.workspaceSearch.SetText(string.Empty);
    }

    /// <summary>
    /// Starts a new chat in a workspace, when the workspace tree asks for one.
    /// </summary>
    /// <remarks>
    /// The page reads its reference to the chat component only now, when the user clicked. The chat
    /// component is created anew whenever the workspace sidebar is shown or hidden, so a reference
    /// taken earlier could point to one which is gone.
    /// </remarks>
    /// <param name="workspaceId">The ID of the workspace.</param>
    private async Task StartNewChatInWorkspace(Guid workspaceId)
    {
        if (this.chatComponent is null)
            return;

        await this.chatComponent.StartNewChatInWorkspaceAsync(workspaceId);
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.dotNetReference?.Dispose();
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