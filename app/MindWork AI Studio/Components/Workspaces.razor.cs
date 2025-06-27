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
    private ThreadSafeRandom RNG { get; init; } = null!;
    
    [Inject]
    private ILogger<Workspaces> Logger { get; init; } = null!;
    
    [Parameter]
    public ChatThread? CurrentChatThread { get; set; }
    
    [Parameter]
    public EventCallback<ChatThread> CurrentChatThreadChanged { get; set; }

    [Parameter]
    public bool ExpandRootNodes { get; set; } = true;

    private const Placement WORKSPACE_ITEM_TOOLTIP_PLACEMENT = Placement.Bottom;

    private readonly List<TreeItemData<ITreeItem>> treeItems = new();
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        //
        // Notice: In order to get the server-based loading to work, we need to respect the following rules:
        // - We must have initial tree items
        // - Those initial tree items cannot have children
        // - When assigning the tree items to the MudTreeViewItem component, we must set the Value property to the value of the item
        //
        // We won't await the loading of the tree items here,
        // to avoid blocking the UI thread:
        _ = this.LoadTreeItems();
    }

    #endregion

    private async Task LoadTreeItems()
    {
        this.treeItems.Clear();
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
                Children = await this.LoadWorkspaces(),
            },
        });
        
        this.treeItems.Add(new TreeItemData<ITreeItem>
        {
            Expandable = false,
            Value = new TreeDivider(),
        });
        
        await this.InvokeAsync(this.StateHasChanged);
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
                Children = await this.LoadTemporaryChats(),
            },
        });
        
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task<IReadOnlyCollection<TreeItemData<ITreeItem>>> LoadTemporaryChats()
    {
        var tempChildren = new List<TreeItemData>();
        
        // Get the temp root directory: 
        var temporaryDirectories = Path.Join(SettingsManager.DataDirectory, "tempChats");
                        
        // Ensure the directory exists:
        Directory.CreateDirectory(temporaryDirectories);
                        
        // Enumerate the chat directories:
        foreach (var tempChatDirPath in Directory.EnumerateDirectories(temporaryDirectories))
        {
            // Read the `name` file:
            var chatNamePath = Path.Join(tempChatDirPath, "name");
            var chatName = await File.ReadAllTextAsync(chatNamePath, Encoding.UTF8);
            
            // Read the last change time of the chat:
            var chatThreadPath = Path.Join(tempChatDirPath, "thread.json");
            var lastEditTime = File.GetLastWriteTimeUtc(chatThreadPath);
            
            tempChildren.Add(new TreeItemData
            {
                Type = TreeItemType.CHAT,
                Depth = 1,
                Branch = WorkspaceBranch.TEMPORARY_CHATS,
                Text = chatName,
                Icon = Icons.Material.Filled.Timer,
                Expandable = false,
                Path = tempChatDirPath,
                LastEditTime = lastEditTime,
            });
        }
        
        var result = new List<TreeItemData<ITreeItem>>(tempChildren.OrderByDescending(n => n.LastEditTime).Select(n => new TreeItemData<ITreeItem>
        {
            Expandable = false,
            Value = n,
        }));
        return result;
    }
    
    private async Task<IReadOnlyCollection<TreeItemData<ITreeItem>>> LoadWorkspaces()
    {
        var workspaces = new List<TreeItemData<ITreeItem>>();
        
        //
        // Search for workspace folders in the data directory:
        //

        // Get the workspace root directory: 
        var workspaceDirectories = Path.Join(SettingsManager.DataDirectory, "workspaces");

        // Ensure the directory exists:
        Directory.CreateDirectory(workspaceDirectories);

        // Enumerate the workspace directories:
        foreach (var workspaceDirPath in Directory.EnumerateDirectories(workspaceDirectories))
        {
            // Read the `name` file:
            var workspaceNamePath = Path.Join(workspaceDirPath, "name");
            var workspaceName = await File.ReadAllTextAsync(workspaceNamePath, Encoding.UTF8);
                                
            workspaces.Add(new TreeItemData<ITreeItem>
            {
                Expandable = true,
                Value = new TreeItemData
                {
                    Type = TreeItemType.WORKSPACE,
                    Depth = 1,
                    Branch = WorkspaceBranch.WORKSPACES,
                    Text = workspaceName,
                    Icon = Icons.Material.Filled.Description,
                    Expandable = true,
                    Path = workspaceDirPath,
                    Children = await this.LoadWorkspaceChats(workspaceDirPath),
                },
            });
        }
                            
        workspaces.Add(new TreeItemData<ITreeItem>
        {
            Expandable = false,
            Value = new TreeButton(WorkspaceBranch.WORKSPACES, 1, T("Add workspace"),Icons.Material.Filled.LibraryAdd, this.AddWorkspace),
        });
        return workspaces;
    }

    private async Task<IReadOnlyCollection<TreeItemData<ITreeItem>>> LoadWorkspaceChats(string workspacePath)
    {
        var workspaceChats = new List<TreeItemData>();
        
        // Enumerate the workspace directory:
        foreach (var chatPath in Directory.EnumerateDirectories(workspacePath))
        {
            // Read the `name` file:
            var chatNamePath = Path.Join(chatPath, "name");
            var chatName = await File.ReadAllTextAsync(chatNamePath, Encoding.UTF8);
            
            // Read the last change time of the chat:
            var chatThreadPath = Path.Join(chatPath, "thread.json");
            var lastEditTime = File.GetLastWriteTimeUtc(chatThreadPath);
                                
            workspaceChats.Add(new TreeItemData
            {
                Type = TreeItemType.CHAT,
                Depth = 2,
                Branch = WorkspaceBranch.WORKSPACES,
                Text = chatName,
                Icon = Icons.Material.Filled.Chat,
                Expandable = false,
                Path = chatPath,
                LastEditTime = lastEditTime,
            });
        }
        
        var result = new List<TreeItemData<ITreeItem>>(workspaceChats.OrderByDescending(n => n.LastEditTime).Select(n => new TreeItemData<ITreeItem>
        {
            Expandable = false,
            Value = n,
        }));
        
        result.Add(new()
        {
            Expandable = false,
            Value = new TreeButton(WorkspaceBranch.WORKSPACES, 2, T("Add chat"),Icons.Material.Filled.AddComment, () => this.AddChat(workspacePath)),
        });
        
        return result;
    }

    public async Task StoreChat(ChatThread chat, bool reloadTreeItems = true)
    {
        await WorkspaceBehaviour.StoreChat(chat);
        
        // Reload the tree items:
        if(reloadTreeItems)
            await this.LoadTreeItems();
        
        this.StateHasChanged();
    }

    private async Task<ChatThread?> LoadChat(string? chatPath, bool switchToChat)
    {
        if(string.IsNullOrWhiteSpace(chatPath))
            return null;

        if(!Directory.Exists(chatPath))
            return null;
        
        // Check if the chat has unsaved changes:
        if (switchToChat && await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(this, Event.HAS_CHAT_UNSAVED_CHANGES))
        {
            var dialogParameters = new DialogParameters
            {
                { "Message", T("Are you sure you want to load another chat? All unsaved changes will be lost.") },
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

    public async Task DeleteChat(string? chatPath, bool askForConfirmation = true, bool unloadChat = true)
    {
        var chat = await this.LoadChat(chatPath, false);
        if (chat is null)
            return;

        if (askForConfirmation)
        {
            var workspaceName = await WorkspaceBehaviour.LoadWorkspaceName(chat.WorkspaceId);
            var dialogParameters = new DialogParameters
            {
                {
                    "Message", (chat.WorkspaceId == Guid.Empty) switch
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

        string chatDirectory;
        if (chat.WorkspaceId == Guid.Empty)
            chatDirectory = Path.Join(SettingsManager.DataDirectory, "tempChats", chat.ChatId.ToString());
        else
            chatDirectory = Path.Join(SettingsManager.DataDirectory, "workspaces", chat.WorkspaceId.ToString(), chat.ChatId.ToString());

        Directory.Delete(chatDirectory, true);
        await this.LoadTreeItems();
        
        if(unloadChat && this.CurrentChatThread?.ChatId == chat.ChatId)
        {
            this.CurrentChatThread = null;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
            await MessageBus.INSTANCE.SendMessage<bool>(this, Event.WORKSPACE_LOADED_CHAT_CHANGED);
        }
    }

    private async Task RenameChat(string? chatPath)
    {
        var chat = await this.LoadChat(chatPath, false);
        if (chat is null)
            return;
        
        var dialogParameters = new DialogParameters
        {
            { "Message", string.Format(T("Please enter a new or edit the name for your chat '{0}':"), chat.Name) },
            { "UserInput", chat.Name },
            { "ConfirmText", T("Rename") },
            { "ConfirmColor", Color.Info },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Rename Chat"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        chat.Name = (dialogResult.Data as string)!;
        if(this.CurrentChatThread?.ChatId == chat.ChatId)
        {
            this.CurrentChatThread.Name = chat.Name;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
            await MessageBus.INSTANCE.SendMessage<bool>(this, Event.WORKSPACE_LOADED_CHAT_CHANGED);
        }
        
        await this.StoreChat(chat);
        await this.LoadTreeItems();
    }
    
    private async Task RenameWorkspace(string? workspacePath)
    {
        if(workspacePath is null)
            return;
        
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        var workspaceName = await WorkspaceBehaviour.LoadWorkspaceName(workspaceId);
        var dialogParameters = new DialogParameters
        {
            { "Message", string.Format(T("Please enter a new or edit the name for your workspace '{0}':"), workspaceName) },
            { "UserInput", workspaceName },
            { "ConfirmText", T("Rename") },
            { "ConfirmColor", Color.Info },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Rename Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var alteredWorkspaceName = (dialogResult.Data as string)!;
        var workspaceNamePath = Path.Join(workspacePath, "name");
        await File.WriteAllTextAsync(workspaceNamePath, alteredWorkspaceName, Encoding.UTF8);
        await this.LoadTreeItems();
    }

    private async Task AddWorkspace()
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", T("Please name your workspace:") },
            { "UserInput", string.Empty },
            { "ConfirmText", T("Add workspace") },
            { "ConfirmColor", Color.Info },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Add Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceId = Guid.NewGuid();
        var workspacePath = Path.Join(SettingsManager.DataDirectory, "workspaces", workspaceId.ToString());
        Directory.CreateDirectory(workspacePath);
        
        var workspaceNamePath = Path.Join(workspacePath, "name");
        await File.WriteAllTextAsync(workspaceNamePath, (dialogResult.Data as string)!, Encoding.UTF8);
        
        await this.LoadTreeItems();
    }

    private async Task DeleteWorkspace(string? workspacePath)
    {
        if(workspacePath is null)
            return;
        
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        var workspaceName = await WorkspaceBehaviour.LoadWorkspaceName(workspaceId);
        
        // Determine how many chats are in the workspace:
        var chatCount = Directory.EnumerateDirectories(workspacePath).Count();
        
        var dialogParameters = new DialogParameters
        {
            { "Message", string.Format(T("Are you sure you want to delete the workspace '{0}'? This will also delete {1} chat(s) in this workspace."), workspaceName, chatCount) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        Directory.Delete(workspacePath, true);
        await this.LoadTreeItems();
    }
    
    private async Task MoveChat(string? chatPath)
    {
        var chat = await this.LoadChat(chatPath, false);
        if (chat is null)
            return;
        
        var dialogParameters = new DialogParameters
        {
            { "Message", T("Please select the workspace where you want to move the chat to.") },
            { "SelectedWorkspace", chat.WorkspaceId },
            { "ConfirmText", T("Move chat") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<WorkspaceSelectionDialog>(T("Move Chat to Workspace"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceId = dialogResult.Data is Guid id ? id : Guid.Empty;
        if (workspaceId == Guid.Empty)
            return;
        
        // Delete the chat from the current workspace or the temporary storage:
        if (chat.WorkspaceId == Guid.Empty)
        {
            // Case: The chat is stored in the temporary storage:
            await this.DeleteChat(Path.Join(SettingsManager.DataDirectory, "tempChats", chat.ChatId.ToString()), askForConfirmation: false, unloadChat: false);
        }
        else
        {
            // Case: The chat is stored in a workspace.
            await this.DeleteChat(Path.Join(SettingsManager.DataDirectory, "workspaces", chat.WorkspaceId.ToString(), chat.ChatId.ToString()), askForConfirmation: false, unloadChat: false);
        }
        
        // Update the chat's workspace:
        chat.WorkspaceId = workspaceId;
        
        // Handle the case where the chat is the active chat:
        if (this.CurrentChatThread?.ChatId == chat.ChatId)
        {
            this.CurrentChatThread = chat;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
            await MessageBus.INSTANCE.SendMessage<bool>(this, Event.WORKSPACE_LOADED_CHAT_CHANGED);
        }
        
        await this.StoreChat(chat);
    }
    
    private async Task AddChat(string workspacePath)
    {
        // Check if the chat has unsaved changes:
        if (await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(this, Event.HAS_CHAT_UNSAVED_CHANGES))
        {
            var dialogParameters = new DialogParameters
            {
                { "Message", T("Are you sure you want to create a another chat? All unsaved changes will be lost.") },
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
            Seed = this.RNG.Next(),
            SystemPrompt = SystemPrompts.DEFAULT,
            Blocks = [],
        };
        
        var chatPath = Path.Join(workspacePath, chat.ChatId.ToString());
        
        await this.StoreChat(chat);
        await this.LoadChat(chatPath, switchToChat: true);
        await this.LoadTreeItems();
    }

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
                await this.LoadTreeItems();
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }

    #endregion
}