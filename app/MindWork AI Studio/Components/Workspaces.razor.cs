using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class Workspaces : ComponentBase
{
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
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
    public Func<Task> LoadedChatWasChanged { get; set; } = () => Task.CompletedTask;

    private const Placement WORKSPACE_ITEM_TOOLTIP_PLACEMENT = Placement.Bottom;
    
    public static readonly Guid WORKSPACE_ID_BIAS = Guid.Parse("82050a4e-ee92-43d7-8ee5-ab512f847e02");
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper),
        }
    };

    private readonly List<TreeItemData<ITreeItem>> treeItems = new();
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        //
        // Notice: In order to get the server-based loading to work, we need to respect the following rules:
        // - We must have initial tree items
        // - Those initial tree items cannot have children
        // - When assigning the tree items to the MudTreeViewItem component, we must set the Value property to the value of the item
        //
        
        await this.EnsureBiasWorkspace();
        await this.LoadTreeItems();
        await base.OnInitializedAsync();
    }

    #endregion

    private async Task LoadTreeItems()
    {
        this.treeItems.Clear();
        this.treeItems.Add(new TreeItemData<ITreeItem>
        {
            Expandable = true,
            Value = new TreeItemData
            {
                Depth = 0,
                Branch = WorkspaceBranch.WORKSPACES,
                Text = "Workspaces",
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
        
        this.treeItems.Add(new TreeItemData<ITreeItem>
        {
            Expandable = true,
            Value = new TreeItemData
            {
                Depth = 0,
                Branch = WorkspaceBranch.TEMPORARY_CHATS,
                Text = "Temporary chats",
                Icon = Icons.Material.Filled.Timer,
                Expandable = true,
                Path = "temp",
                Children = await this.LoadTemporaryChats(),
            },
        });
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
    
    public async Task<string> LoadWorkspaceName(Guid workspaceId)
    {
        if(workspaceId == Guid.Empty)
            return string.Empty;
        
        var workspacePath = Path.Join(SettingsManager.DataDirectory, "workspaces", workspaceId.ToString());
        var workspaceNamePath = Path.Join(workspacePath, "name");
        return await File.ReadAllTextAsync(workspaceNamePath, Encoding.UTF8);
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
            Value = new TreeButton(WorkspaceBranch.WORKSPACES, 1, "Add workspace",Icons.Material.Filled.LibraryAdd, this.AddWorkspace),
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
            Value = new TreeButton(WorkspaceBranch.WORKSPACES, 2, "Add chat",Icons.Material.Filled.AddComment, () => this.AddChat(workspacePath)),
        });
        
        return result;
    }

    public async Task StoreChat(ChatThread chat, bool reloadTreeItems = true)
    {
        string chatDirectory;
        if (chat.WorkspaceId == Guid.Empty)
            chatDirectory = Path.Join(SettingsManager.DataDirectory, "tempChats", chat.ChatId.ToString());
        else
            chatDirectory = Path.Join(SettingsManager.DataDirectory, "workspaces", chat.WorkspaceId.ToString(), chat.ChatId.ToString());
        
        // Ensure the directory exists:
        Directory.CreateDirectory(chatDirectory);
        
        // Save the chat name:
        var chatNamePath = Path.Join(chatDirectory, "name");
        await File.WriteAllTextAsync(chatNamePath, chat.Name);
        
        // Save the thread as thread.json:
        var chatPath = Path.Join(chatDirectory, "thread.json");
        await File.WriteAllTextAsync(chatPath, JsonSerializer.Serialize(chat, JSON_OPTIONS), Encoding.UTF8);
        
        // Reload the tree items:
        if(reloadTreeItems)
            await this.LoadTreeItems();
        
        this.StateHasChanged();
    }
    
    public async Task LoadChat(LoadChat loadChat)
    {
        var chatPath = loadChat.WorkspaceId == Guid.Empty
            ? Path.Join(SettingsManager.DataDirectory, "tempChats", loadChat.ChatId.ToString())
            : Path.Join(SettingsManager.DataDirectory, "workspaces", loadChat.WorkspaceId.ToString(), loadChat.ChatId.ToString());
        
        await this.LoadChat(chatPath, switchToChat: true);
    }
    
    public static bool IsChatExisting(LoadChat loadChat)
    {
        var chatPath = loadChat.WorkspaceId == Guid.Empty
            ? Path.Join(SettingsManager.DataDirectory, "tempChats", loadChat.ChatId.ToString())
            : Path.Join(SettingsManager.DataDirectory, "workspaces", loadChat.WorkspaceId.ToString(), loadChat.ChatId.ToString());
        
        return Directory.Exists(chatPath);
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
                { "Message", "Are you sure you want to load another chat? All unsaved changes will be lost." },
            };
        
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Load Chat", dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return null;
        }

        try
        {
            var chatData = await File.ReadAllTextAsync(Path.Join(chatPath, "thread.json"), Encoding.UTF8);
            var chat = JsonSerializer.Deserialize<ChatThread>(chatData, JSON_OPTIONS);
            if (switchToChat)
            {
                this.CurrentChatThread = chat;
                await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
                await this.LoadedChatWasChanged();
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
            var workspaceName = await this.LoadWorkspaceName(chat.WorkspaceId);
            var dialogParameters = new DialogParameters
            {
                {
                    "Message", (chat.WorkspaceId == Guid.Empty) switch
                    {
                        true => $"Are you sure you want to delete the temporary chat '{chat.Name}'?",
                        false => $"Are you sure you want to delete the chat '{chat.Name}' in the workspace '{workspaceName}'?",
                    }
                },
            };

            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Chat", dialogParameters, DialogOptions.FULLSCREEN);
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
            await this.LoadedChatWasChanged();
        }
    }

    private async Task RenameChat(string? chatPath)
    {
        var chat = await this.LoadChat(chatPath, false);
        if (chat is null)
            return;
        
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Please enter a new or edit the name for your chat '{chat.Name}':" },
            { "UserInput", chat.Name },
            { "ConfirmText", "Rename" },
            { "ConfirmColor", Color.Info },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>("Rename Chat", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        chat.Name = (dialogResult.Data as string)!;
        await this.StoreChat(chat);
        await this.LoadTreeItems();
    }
    
    private async Task RenameWorkspace(string? workspacePath)
    {
        if(workspacePath is null)
            return;
        
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        var workspaceName = await this.LoadWorkspaceName(workspaceId);
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Please enter a new or edit the name for your workspace '{workspaceName}':" },
            { "UserInput", workspaceName },
            { "ConfirmText", "Rename" },
            { "ConfirmColor", Color.Info },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>("Rename Workspace", dialogParameters, DialogOptions.FULLSCREEN);
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
            { "Message", "Please name your workspace:" },
            { "UserInput", string.Empty },
            { "ConfirmText", "Add workspace" },
            { "ConfirmColor", Color.Info },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>("Add Workspace", dialogParameters, DialogOptions.FULLSCREEN);
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

    private async Task EnsureBiasWorkspace()
    {
        var workspacePath = Path.Join(SettingsManager.DataDirectory, "workspaces", WORKSPACE_ID_BIAS.ToString());
        
        if(Path.Exists(workspacePath))
            return;
        
        Directory.CreateDirectory(workspacePath);
        var workspaceNamePath = Path.Join(workspacePath, "name");
        await File.WriteAllTextAsync(workspaceNamePath, "Bias of the Day", Encoding.UTF8);
    }
    
    private async Task DeleteWorkspace(string? workspacePath)
    {
        if(workspacePath is null)
            return;
        
        var workspaceId = Guid.Parse(Path.GetFileName(workspacePath));
        var workspaceName = await this.LoadWorkspaceName(workspaceId);
        
        // Determine how many chats are in the workspace:
        var chatCount = Directory.EnumerateDirectories(workspacePath).Count();
        
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Are you sure you want to delete the workspace '{workspaceName}'? This will also delete {chatCount} chat(s) in this workspace." },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Workspace", dialogParameters, DialogOptions.FULLSCREEN);
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
            { "Message", "Please select the workspace where you want to move the chat to." },
            { "SelectedWorkspace", chat.WorkspaceId },
            { "ConfirmText", "Move chat" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<WorkspaceSelectionDialog>("Move Chat to Workspace", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceId = dialogResult.Data is Guid id ? id : default;
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
            await this.LoadedChatWasChanged();
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
                { "Message", "Are you sure you want to create a another chat? All unsaved changes will be lost." },
            };
        
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Create Chat", dialogParameters, DialogOptions.FULLSCREEN);
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
            SystemPrompt = "You are a helpful assistant!",
            Blocks = [],
        };
        
        var chatPath = Path.Join(workspacePath, chat.ChatId.ToString());
        
        await this.StoreChat(chat);
        await this.LoadChat(chatPath, switchToChat: true);
        await this.LoadTreeItems();
    }
}