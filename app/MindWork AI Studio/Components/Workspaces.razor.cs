using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class Workspaces : MSGComponentBase
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ILogger<Workspaces> Logger { get; init; } = null!;
    
    [Parameter]
    public ChatThread? CurrentChatThread { get; set; }
    
    [Parameter]
    public EventCallback<ChatThread> CurrentChatThreadChanged { get; set; }

    [Parameter]
    public bool ExpandRootNodes { get; set; } = true;

    private const Placement WORKSPACE_ITEM_TOOLTIP_PLACEMENT = Placement.Bottom;
    private readonly SemaphoreSlim treeLoadingSemaphore = new(1, 1);
    private readonly List<TreeItemData<ITreeItem>> treeItems = [];
    private readonly HashSet<Guid> loadingWorkspaceChatLists = [];

    private CancellationTokenSource? prefetchCancellationTokenSource;
    private bool isInitialLoading = true;
    private bool isDisposed;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _ = this.LoadTreeItemsAsync(startPrefetch: true);
    }

    #endregion

    private async Task LoadTreeItemsAsync(bool startPrefetch = true, bool forceReload = false)
    {
        await this.treeLoadingSemaphore.WaitAsync();
        try
        {
            if (this.isDisposed)
                return;

            if (forceReload)
                await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();

            var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
            this.BuildTreeItems(snapshot);
            this.isInitialLoading = false;
        }
        finally
        {
            this.treeLoadingSemaphore.Release();
        }

        await this.SafeStateHasChanged();

        if (startPrefetch)
            await this.StartPrefetchAsync();
    }

    private void BuildTreeItems(WorkspaceTreeCacheSnapshot snapshot)
    {
        this.treeItems.Clear();

        var workspaceChildren = new List<TreeItemData<ITreeItem>>();
        foreach (var workspace in snapshot.Workspaces)
            workspaceChildren.Add(this.CreateWorkspaceTreeItem(workspace));

        workspaceChildren.Add(new TreeItemData<ITreeItem>
        {
            Expandable = false,
            Value = new TreeButton(WorkspaceBranch.WORKSPACES, 1, T("Add workspace"), Icons.Material.Filled.LibraryAdd, this.AddWorkspaceAsync),
        });

        this.treeItems.Add(new TreeItemData<ITreeItem>
        {
            Expanded = this.ExpandRootNodes,
            Expandable = true,
            Value = new TreeItemData
            {
                Depth = 0,
                Branch = WorkspaceBranch.WORKSPACES,
                Text = T("Workspaces"),
                Icon = Icons.Material.Filled.Folder,
                Expandable = true,
                Path = "root",
                Children = workspaceChildren,
            },
        });
        
        this.treeItems.Add(new TreeItemData<ITreeItem>
        {
            Expandable = false,
            Value = new TreeDivider(),
        });
        
        var temporaryChatsChildren = new List<TreeItemData<ITreeItem>>();
        foreach (var temporaryChat in snapshot.TemporaryChats.OrderByDescending(x => x.LastEditTime))
            temporaryChatsChildren.Add(CreateChatTreeItem(temporaryChat, WorkspaceBranch.TEMPORARY_CHATS, depth: 1, icon: Icons.Material.Filled.Timer));

        this.treeItems.Add(new TreeItemData<ITreeItem>
        {
            Expanded = this.ExpandRootNodes,
            Expandable = true,
            Value = new TreeItemData
            {
                Depth = 0,
                Branch = WorkspaceBranch.TEMPORARY_CHATS,
                Text = T("Disappearing Chats"),
                Icon = Icons.Material.Filled.Timer,
                Expandable = true,
                Path = "temp",
                Children = temporaryChatsChildren,
            },
        });
    }

    private TreeItemData<ITreeItem> CreateWorkspaceTreeItem(WorkspaceTreeWorkspace workspace)
    {
        var children = new List<TreeItemData<ITreeItem>>();
        if (workspace.ChatsLoaded)
        {
            foreach (var workspaceChat in workspace.Chats.OrderByDescending(x => x.LastEditTime))
                children.Add(CreateChatTreeItem(workspaceChat, WorkspaceBranch.WORKSPACES, depth: 2, icon: Icons.Material.Filled.Chat));
        }
        else if (this.loadingWorkspaceChatLists.Contains(workspace.WorkspaceId))
            children.AddRange(this.CreateLoadingRows(workspace.WorkspacePath));

        children.Add(new TreeItemData<ITreeItem>
        {
            Expandable = false,
            Value = new TreeButton(WorkspaceBranch.WORKSPACES, 2, T("Add chat"), Icons.Material.Filled.AddComment, () => this.AddChatAsync(workspace.WorkspacePath)),
        });

        return new TreeItemData<ITreeItem>
        {
            Expandable = true,
            Value = new TreeItemData
            {
                Type = TreeItemType.WORKSPACE,
                Depth = 1,
                Branch = WorkspaceBranch.WORKSPACES,
                Text = workspace.Name,
                Icon = Icons.Material.Filled.Description,
                Expandable = true,
                Path = workspace.WorkspacePath,
                Children = children,
            },
        };
    }

    private IReadOnlyCollection<TreeItemData<ITreeItem>> CreateLoadingRows(string workspacePath)
    {
        return
        [
            this.CreateLoadingTreeItem(workspacePath, "loading_1"),
            this.CreateLoadingTreeItem(workspacePath, "loading_2"),
            this.CreateLoadingTreeItem(workspacePath, "loading_3"),
        ];
    }

    private TreeItemData<ITreeItem> CreateLoadingTreeItem(string workspacePath, string suffix)
    {
        return new TreeItemData<ITreeItem>
        {
            Expandable = false,
            Value = new TreeItemData
            {
                Type = TreeItemType.LOADING,
                Depth = 2,
                Branch = WorkspaceBranch.WORKSPACES,
                Text = T("Loading chats..."),
                Icon = Icons.Material.Filled.HourglassTop,
                Expandable = false,
                Path = Path.Join(workspacePath, suffix),
            },
        };
    }

    private static TreeItemData<ITreeItem> CreateChatTreeItem(WorkspaceTreeChat chat, WorkspaceBranch branch, int depth, string icon)
    {
        return new TreeItemData<ITreeItem>
        {
            Expandable = false,
            Value = new TreeItemData
            {
                Type = TreeItemType.CHAT,
                Depth = depth,
                Branch = branch,
                Text = chat.Name,
                Icon = icon,
                Expandable = false,
                Path = chat.ChatPath,
                LastEditTime = chat.LastEditTime,
            },
        };
    }

    private async Task SafeStateHasChanged()
    {
        if (this.isDisposed)
            return;

        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task StartPrefetchAsync()
    {
        if (this.prefetchCancellationTokenSource is not null)
        {
            await this.prefetchCancellationTokenSource.CancelAsync();
            this.prefetchCancellationTokenSource.Dispose();
        }

        this.prefetchCancellationTokenSource = new CancellationTokenSource();
        await this.PrefetchWorkspaceChatsAsync(this.prefetchCancellationTokenSource.Token);
    }

    private async Task PrefetchWorkspaceChatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WorkspaceBehaviour.TryPrefetchRemainingChatsAsync(async _ =>
            {
                if (this.isDisposed || cancellationToken.IsCancellationRequested)
                    return;

                await this.LoadTreeItemsAsync(startPrefetch: false);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when the component is hidden or disposed.
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed while prefetching workspace chats.");
        }
    }

    private async Task OnWorkspaceClicked(TreeItemData treeItem)
    {
        if (treeItem.Type is not TreeItemType.WORKSPACE)
            return;

        if (!Guid.TryParse(Path.GetFileName(treeItem.Path), out var workspaceId))
            return;

        await this.EnsureWorkspaceChatsLoadedAsync(workspaceId);
    }

    private async Task EnsureWorkspaceChatsLoadedAsync(Guid workspaceId)
    {
        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        var hasWorkspace = false;
        var chatsLoaded = false;
        
        foreach (var workspace in snapshot.Workspaces)
        {
            if (workspace.WorkspaceId != workspaceId)
                continue;

            hasWorkspace = true;
            chatsLoaded = workspace.ChatsLoaded;
            break;
        }

        if (!hasWorkspace || chatsLoaded || !this.loadingWorkspaceChatLists.Add(workspaceId))
            return;

        await this.LoadTreeItemsAsync(startPrefetch: false);

        try
        {
            await WorkspaceBehaviour.GetWorkspaceChatsAsync(workspaceId);
        }
        finally
        {
            this.loadingWorkspaceChatLists.Remove(workspaceId);
        }

        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    public async Task ForceRefreshFromDiskAsync()
    {
        if (this.prefetchCancellationTokenSource is not null)
        {
            await this.prefetchCancellationTokenSource.CancelAsync();
            this.prefetchCancellationTokenSource.Dispose();
            this.prefetchCancellationTokenSource = null;
        }
        
        this.loadingWorkspaceChatLists.Clear();
        this.isInitialLoading = true;
        
        await this.SafeStateHasChanged();
        await this.LoadTreeItemsAsync(startPrefetch: true, forceReload: true);
    }

    public async Task StoreChatAsync(ChatThread chat, bool reloadTreeItems = false)
    {
        await WorkspaceBehaviour.StoreChatAsync(chat);
        
        if (reloadTreeItems)
            this.loadingWorkspaceChatLists.Clear();
        
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    private async Task<ChatThread?> LoadChatAsync(string? chatPath, bool switchToChat)
    {
        if (string.IsNullOrWhiteSpace(chatPath))
            return null;

        if (!Directory.Exists(chatPath))
            return null;
        
        if (switchToChat && await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(this, Event.HAS_CHAT_UNSAVED_CHANGES))
        {
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, T("Are you sure you want to load another chat? All unsaved changes will be lost.") },
            };
            
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Load Chat"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return null;
        }

        try
        {
            var chatData = await File.ReadAllTextAsync(Path.Join(chatPath, "thread.json"), Encoding.UTF8);
            var chat = JsonSerializer.Deserialize<ChatThread>(chatData, WorkspaceBehaviour.JSON_OPTIONS);
            if (switchToChat)
            {
                this.CurrentChatThread = chat;
                await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
                await MessageBus.INSTANCE.SendMessage<bool>(this, Event.WORKSPACE_LOADED_CHAT_CHANGED);
            }
            
            return chat;
        }
        catch (Exception e)
        {
            this.Logger.LogError($"Failed to load chat from '{chatPath}': {e.Message}");
        }
        
        return null;
    }

    public async Task DeleteChatAsync(string? chatPath, bool askForConfirmation = true, bool unloadChat = true)
    {
        var chat = await this.LoadChatAsync(chatPath, false);
        if (chat is null)
            return;

        if (askForConfirmation)
        {
            var workspaceName = await WorkspaceBehaviour.LoadWorkspaceNameAsync(chat.WorkspaceId);
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                {
                    x => x.Message, (chat.WorkspaceId == Guid.Empty) switch
                    {
                        true => string.Format(T("Are you sure you want to delete the temporary chat '{0}'?"), chat.Name),
                        false => string.Format(T("Are you sure you want to delete the chat '{0}' in the workspace '{1}'?"), chat.Name, workspaceName),
                    }
                },
            };

            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Chat"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return;
        }

        await WorkspaceBehaviour.DeleteChatAsync(this.DialogService, chat.WorkspaceId, chat.ChatId, askForConfirmation: false);
        await this.LoadTreeItemsAsync(startPrefetch: false);
        
        if (unloadChat && this.CurrentChatThread?.ChatId == chat.ChatId)
        {
            this.CurrentChatThread = null;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
            await MessageBus.INSTANCE.SendMessage<bool>(this, Event.WORKSPACE_LOADED_CHAT_CHANGED);
        }
    }

    private async Task RenameChatAsync(string? chatPath)
    {
        var chat = await this.LoadChatAsync(chatPath, false);
        if (chat is null)
            return;
        
        var dialogParameters = new DialogParameters<SingleInputDialog>
        {
            { x => x.Message, string.Format(T("Please enter a new or edit the name for your chat '{0}':"), chat.Name) },
            { x => x.InputHeaderText, T("Chat Name") },
            { x => x.UserInput, chat.Name },
            { x => x.ConfirmText, T("Rename") },
            { x => x.ConfirmColor, Color.Info },
            { x => x.AllowEmptyInput, false },
            { x => x.EmptyInputErrorMessage, T("Please enter a chat name.") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Rename Chat"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        chat.Name = (dialogResult.Data as string)!;
        if (this.CurrentChatThread?.ChatId == chat.ChatId)
        {
            this.CurrentChatThread.Name = chat.Name;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
            await MessageBus.INSTANCE.SendMessage<bool>(this, Event.WORKSPACE_LOADED_CHAT_CHANGED);
        }
        
        await WorkspaceBehaviour.StoreChatAsync(chat);
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    private async Task RenameWorkspaceAsync(string? workspacePath)
    {
        if (workspacePath is null)
            return;
        
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        var workspaceName = await WorkspaceBehaviour.LoadWorkspaceNameAsync(workspaceId);
        var dialogParameters = new DialogParameters<SingleInputDialog>
        {
            { x => x.Message, string.Format(T("Please enter a new or edit the name for your workspace '{0}':"), workspaceName) },
            { x => x.InputHeaderText, T("Workspace Name") },
            { x => x.UserInput, workspaceName },
            { x => x.ConfirmText, T("Rename") },
            { x => x.ConfirmColor, Color.Info },
            { x => x.AllowEmptyInput, false },
            { x => x.EmptyInputErrorMessage, T("Please enter a workspace name.") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Rename Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var alteredWorkspaceName = (dialogResult.Data as string)!;
        var workspaceNamePath = Path.Join(workspacePath, "name");
        await File.WriteAllTextAsync(workspaceNamePath, alteredWorkspaceName, Encoding.UTF8);
        await WorkspaceBehaviour.UpdateWorkspaceNameInCacheAsync(workspaceId, alteredWorkspaceName);
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    private async Task AddWorkspaceAsync()
    {
        var dialogParameters = new DialogParameters<SingleInputDialog>
        {
            { x => x.Message, T("Please name your workspace:") },
            { x => x.InputHeaderText, T("Workspace Name") },
            { x => x.UserInput, string.Empty },
            { x => x.ConfirmText, T("Add workspace") },
            { x => x.ConfirmColor, Color.Info },
            { x => x.AllowEmptyInput, false },
            { x => x.EmptyInputErrorMessage, T("Please enter a workspace name.") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Add Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceId = Guid.NewGuid();
        var workspacePath = Path.Join(SettingsManager.DataDirectory, "workspaces", workspaceId.ToString());
        Directory.CreateDirectory(workspacePath);
        
        var workspaceName = (dialogResult.Data as string)!;
        var workspaceNamePath = Path.Join(workspacePath, "name");
        await File.WriteAllTextAsync(workspaceNamePath, workspaceName, Encoding.UTF8);
        await WorkspaceBehaviour.AddWorkspaceToCacheAsync(workspaceId, workspacePath, workspaceName);
        
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    private async Task DeleteWorkspaceAsync(string? workspacePath)
    {
        if (workspacePath is null)
            return;
        
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        var workspaceName = await WorkspaceBehaviour.LoadWorkspaceNameAsync(workspaceId);
        
        var chatCount = Directory.EnumerateDirectories(workspacePath).Count();
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the workspace '{0}'? This will also delete {1} chat(s) in this workspace."), workspaceName, chatCount) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        Directory.Delete(workspacePath, true);
        await WorkspaceBehaviour.RemoveWorkspaceFromCacheAsync(workspaceId);
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }
    
    private async Task MoveChatAsync(string? chatPath)
    {
        var chat = await this.LoadChatAsync(chatPath, false);
        if (chat is null)
            return;
        
        var dialogParameters = new DialogParameters<WorkspaceSelectionDialog>
        {
            { x => x.Message, T("Please select the workspace where you want to move the chat to.") },
            { x => x.SelectedWorkspace, chat.WorkspaceId },
            { x => x.ConfirmText, T("Move chat") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<WorkspaceSelectionDialog>(T("Move Chat to Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceId = dialogResult.Data is Guid id ? id : Guid.Empty;
        if (workspaceId == Guid.Empty)
            return;
        
        await WorkspaceBehaviour.DeleteChatAsync(this.DialogService, chat.WorkspaceId, chat.ChatId, askForConfirmation: false);
        
        chat.WorkspaceId = workspaceId;
        if (this.CurrentChatThread?.ChatId == chat.ChatId)
        {
            this.CurrentChatThread = chat;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
            await MessageBus.INSTANCE.SendMessage<bool>(this, Event.WORKSPACE_LOADED_CHAT_CHANGED);
        }
        
        await WorkspaceBehaviour.StoreChatAsync(chat);
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }
    
    private async Task AddChatAsync(string workspacePath)
    {
        if (await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(this, Event.HAS_CHAT_UNSAVED_CHANGES))
        {
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, T("Are you sure you want to create a another chat? All unsaved changes will be lost.") },
            };
        
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Create Chat"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return;
        }
        
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        var chat = new ChatThread
        {
            WorkspaceId = workspaceId,
            ChatId = Guid.NewGuid(),
            Name = string.Empty,
            SystemPrompt = SystemPrompts.DEFAULT,
            Blocks = [],
        };
        
        var chatPath = Path.Join(workspacePath, chat.ChatId.ToString());
        
        await WorkspaceBehaviour.StoreChatAsync(chat);
        await this.LoadChatAsync(chatPath, switchToChat: true);
        await this.LoadTreeItemsAsync(startPrefetch: false);
    }

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
                await this.ForceRefreshFromDiskAsync();
                break;
        }
    }

    protected override void DisposeResources()
    {
        this.isDisposed = true;
        this.prefetchCancellationTokenSource?.Cancel();
        this.prefetchCancellationTokenSource?.Dispose();
        this.prefetchCancellationTokenSource = null;

        base.DisposeResources();
    }

    #endregion
}