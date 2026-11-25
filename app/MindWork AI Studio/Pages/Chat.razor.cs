using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

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
    private readonly WorkspaceSearch workspaceSearch = new();
    private Workspaces? workspaces;
    private ChatComponent? chatComponent;
    private readonly ChatComposerState composerState = new();

    /// <summary>
    /// Whether the open chat holds changes the user did not save yet, which the chat component tells the
    /// workspace sidebar. The page keeps it, since the sidebar is created anew whenever it shows up again.
    /// </summary>
    private readonly ChatSaveState chatSaveState = new();

    private ElementReference columnsElement;
    private ElementReference sidebarElement;
    private ElementReference sidebarHandle;

    /// <summary>
    /// Whether the script lets the user drag the handle beside the sidebar shown now.
    /// </summary>
    private bool isSidebarResizeWatched;

    /// <summary>
    /// Whether the window is too narrow for the list beside the chat, which the script measures, see
    /// watchChatList in app.js.
    /// </summary>
    private bool isWindowNarrow;

    /// <summary>
    /// Whether the list floats over the chat, and why.
    /// </summary>
    private ChatListFloat listFloat = ChatListFloat.NONE;

    /// <summary>
    /// The part of the path above the chat which the list still has to show, or null, see
    /// <see cref="this.ShowInList"/>.
    /// </summary>
    private BreadcrumbPart? partToShowInList;

    private DotNetObjectReference<Chat>? dotNetReference;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            this.dotNetReference ??= DotNetObjectReference.Create(this);

            //
            // Without the script, the list keeps standing beside the chat, as before. An exception from
            // here would end the whole circuit, though, see below:
            //
            try
            {
                await this.JsRuntime.InvokeVoidAsync("watchChatList", this.columnsElement, this.dotNetReference);
            }
            catch (JSDisconnectedException)
            {
                // Nobody is left to see the list.
            }
            catch (JSException ex)
            {
                this.Logger.LogWarning(ex, "Failed to let the workspace sidebar float over the chat.");
            }
        }

        //
        // The sidebar and its handle are created anew whenever the sidebar shows up again, and the
        // script then has to watch the new handle:
        //
        if (!this.IsSidebarShown)
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

        //
        // A list which a click on the path above the chat just showed exists only after this render,
        // so it learns only now what to show. Until the sidebar shows, the reference still points to
        // the list it showed before:
        //
        if (this.partToShowInList is { } part && this.IsSidebarShown && this.workspaces is not null)
        {
            this.partToShowInList = null;
            await this.workspaces.RevealAsync(part);
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion
    
    /// <summary>
    /// Whether the bar offers to show and hide the list: whenever the list has something to show, unless
    /// the user wants to see it all the time and the window leaves room for it beside the chat.
    /// </summary>
    private bool IsSidebarToggleShown => this.SettingsManager.ConfigurationData.Workspace.IsChatListShown
                                         && (this.SettingsManager.ConfigurationData.Workspace.DisplayBehavior is WorkspaceDisplayBehavior.TOGGLE_SIDEBAR || this.isWindowNarrow);

    /// <summary>
    /// Whether the list stays in sight until the user hides it: beside the chat, or over it after a click
    /// on the toggle. A list which only peeks does not, and a click on the toggle keeps it.
    /// </summary>
    private bool IsSidebarKept => this.IsSidebarDocked || (this.IsSidebarFloating && this.listFloat is ChatListFloat.OPEN);

    private string WorkspaceSidebarToggleIcon => this.IsSidebarKept ? AppIcons.HIDE_SIDEBAR : AppIcons.SHOW_SIDEBAR;

    private string WorkspaceSidebarToggleTooltip => this.IsSidebarKept ? TA("Hide your workspaces") : TA("Show your workspaces");

    /// <summary>
    /// The workspace of the open chat, which the menu of New chat marks, or Guid.Empty outside of workspaces.
    /// </summary>
    private Guid CurrentWorkspaceId => this.chatThread?.WorkspaceId ?? Guid.Empty;

    /// <summary>
    /// Whether the sidebar shows beside the chat. It shows whenever it has something to list, the stored
    /// chats or the workspaces, the user did not hide it, and the window leaves room for it beside the chat.
    /// </summary>
    private bool IsSidebarDocked => !this.isWindowNarrow
                                    && this.SettingsManager.ConfigurationData.Workspace.IsChatListShown
                                    && ((this.SettingsManager.ConfigurationData.Workspace.DisplayBehavior is WorkspaceDisplayBehavior.TOGGLE_SIDEBAR && this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible)
                                    || this.SettingsManager.ConfigurationData.Workspace.DisplayBehavior is WorkspaceDisplayBehavior.SIDEBAR_ALWAYS_VISIBLE);

    /// <summary>
    /// Whether the list floats over the chat. It does so only where the toggle could show it as well.
    /// </summary>
    private bool IsSidebarFloating => this.listFloat is not ChatListFloat.NONE && this.IsSidebarToggleShown && !this.IsSidebarDocked;

    /// <summary>
    /// Whether the list shows, beside the chat or over it.
    /// </summary>
    private bool IsSidebarShown => this.IsSidebarDocked || this.IsSidebarFloating;

    private string SidebarClasses => !this.IsSidebarFloating
        ? "chat-sidebar"
        : this.listFloat is ChatListFloat.PEEK
            ? "chat-sidebar is-floating is-peeking"
            : "chat-sidebar is-floating";

    private async Task ToggleWorkspaceSidebar()
    {
        //
        // In a window too narrow for the list beside the chat, the toggle shows the list over the chat
        // and hides it again. What the user chose for a wider window stays as it is:
        //
        if (this.isWindowNarrow)
        {
            this.listFloat = this.listFloat is ChatListFloat.OPEN ? ChatListFloat.NONE : ChatListFloat.OPEN;
            return;
        }

        //
        // A click while the list peeks keeps it: the list moves beside the chat, as the same element,
        // which therefore keeps what the user sees in it:
        //
        this.listFloat = ChatListFloat.NONE;
        this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible = !this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible;
        await this.SettingsManager.StoreSettings();
    }

    /// <summary>
    /// Shows the open chat or its place in the list, once the user clicked that part of the path above
    /// the chat.
    /// </summary>
    /// <remarks>
    /// A hidden list shows up first, as a click on the toggle shows it: over the chat in a window too narrow
    /// for it, and beside the chat otherwise. A list which only peeks stays as well. The list may come into
    /// being only with the next render, so it learns what to show after that, see
    /// <see cref="this.OnAfterRenderAsync"/>.
    /// </remarks>
    /// <param name="part">The part the user clicked.</param>
    private async Task ShowInList(BreadcrumbPart part)
    {
        if (!this.SettingsManager.ConfigurationData.Workspace.IsChatListShown)
            return;

        if (!this.IsSidebarKept)
        {
            if (this.isWindowNarrow)
                this.listFloat = ChatListFloat.OPEN;
            else
            {
                this.listFloat = ChatListFloat.NONE;
                this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible = true;
                await this.SettingsManager.StoreSettings();
            }
        }

        this.partToShowInList = part;
    }

    /// <summary>
    /// Learns whether the window is too narrow for the list beside the chat, whenever that changes.
    /// </summary>
    /// <remarks>
    /// A list which floats over the chat then closes. In a wider window, the list stands beside the chat
    /// again as the user chose, and in a narrower one, it waits for the toggle.
    /// </remarks>
    /// <param name="isNarrow">Whether the window is too narrow.</param>
    [JSInvokable]
    public void SetWindowNarrow(bool isNarrow)
    {
        if (this.isWindowNarrow == isNarrow)
            return;

        this.isWindowNarrow = isNarrow;
        this.listFloat = ChatListFloat.NONE;
        this.StateHasChanged();
    }

    /// <summary>
    /// Lets the hidden list peek over the chat, once the pointer rested on the toggle for a moment.
    /// </summary>
    [JSInvokable]
    public void PeekAtList()
    {
        if (!this.IsSidebarToggleShown || this.IsSidebarShown)
            return;

        this.listFloat = ChatListFloat.PEEK;
        this.StateHasChanged();
    }

    /// <summary>
    /// Hides the list which peeks over the chat, once the pointer left it and the toggle. A list which the
    /// user kept by a click stays.
    /// </summary>
    [JSInvokable]
    public void EndListPeek()
    {
        if (this.listFloat is not ChatListFloat.PEEK)
            return;

        this.listFloat = ChatListFloat.NONE;
        this.StateHasChanged();
    }

    /// <summary>
    /// Hides the list which floats over the chat, once the user clicked outside of it.
    /// </summary>
    [JSInvokable]
    public void CloseFloatingList()
    {
        if (this.listFloat is ChatListFloat.NONE)
            return;

        this.listFloat = ChatListFloat.NONE;
        this.StateHasChanged();
    }

    /// <summary>
    /// Takes the chat the list opened, or null once the list closed the open chat, such as by deleting it.
    /// </summary>
    /// <remarks>
    /// A list which a click showed over the chat has done its job once the user opened another chat, and
    /// makes way for it. A list which only peeks stays until the pointer leaves it, so the user may look
    /// at one chat after another.
    /// </remarks>
    /// <param name="chat">The chat which is open now.</param>
    private void TakeChatFromList(ChatThread? chat)
    {
        var isAnotherChat = chat is not null && chat.ChatId != this.chatThread?.ChatId;
        this.chatThread = chat;
        if (isAnotherChat && this.listFloat is ChatListFloat.OPEN)
            this.listFloat = ChatListFloat.NONE;
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

    private Task OpenChatSettingsDialog() => SettingsWindow.OpenAsync(this.DialogService, SettingsPath.CHAT);

    /// <summary>
    /// Starts a new chat in a workspace or outside of workspaces, when the workspace tree asks for one.
    /// </summary>
    /// <remarks>
    /// The page reads its reference to the chat component only now, when the user clicked, since the
    /// reference arrives only once the chat component rendered. A list which a click showed over the
    /// chat makes way for the new chat, as it does for a chat the user opened in it.
    /// </remarks>
    /// <param name="workspaceId">The ID of the workspace, or Guid.Empty for a chat outside of workspaces.</param>
    private async Task StartNewChatInPlace(Guid workspaceId)
    {
        if (this.chatComponent is null)
            return;

        await this.chatComponent.StartNewChatInPlaceAsync(workspaceId);
        if (this.listFloat is ChatListFloat.OPEN)
            this.listFloat = ChatListFloat.NONE;
    }

    /// <summary>
    /// Saves the open chat, when the workspace sidebar asks for it.
    /// </summary>
    /// <remarks>
    /// Like <see cref="this.StartNewChatInPlace"/>, this reads the reference to the chat component only
    /// when the user clicked.
    /// </remarks>
    private async Task SaveCurrentChat()
    {
        if (this.chatComponent is null)
            return;

        await this.chatComponent.SaveCurrentChatAsync();
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
            //
            // Whether chats are stored and whether workspaces are used decide whether the sidebar shows,
            // and the user may change both in the settings window while the chat page lies behind it.
            // A list which floated over the chat and now stands beside it floats no more:
            //
            case Event.CONFIGURATION_CHANGED:
                if (this.IsSidebarDocked)
                    this.listFloat = ChatListFloat.NONE;

                this.StateHasChanged();
                break;
        }

        return Task.CompletedTask;
    }
}