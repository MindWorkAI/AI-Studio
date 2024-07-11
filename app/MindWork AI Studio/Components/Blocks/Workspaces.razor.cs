using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Chat;
using AIStudio.Components.CommonDialogs;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Components.CommonDialogs.DialogOptions;

namespace AIStudio.Components.Blocks;

public partial class Workspaces : ComponentBase
{
    [Inject]
    private SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; set; } = null!;
    
    [Parameter]
    public ChatThread? CurrentChatThread { get; set; }
    
    [Parameter]
    public EventCallback<ChatThread> CurrentChatThreadChanged { get; set; }

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
    
    private readonly HashSet<ITreeItem> treeItems = new();
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        //
        // Notice: In order to get the server-based loading to work, we need to respect the following rules:
        // - We must have initial tree items
        // - Those initial tree items cannot have children
        // - When assigning the tree items to the MudTreeViewItem component, we must set the Value property to the value of the item
        //
        
        await this.LoadTreeItems();
        await base.OnInitializedAsync();
    }

    #endregion

    private async Task LoadTreeItems()
    {
        this.treeItems.Clear();
        this.treeItems.Add(new TreeItemData
        {
            Depth = 0,
            Branch = WorkspaceBranch.WORKSPACES,
            Text = "Workspaces",
            Icon = Icons.Material.Filled.Folder,
            Expandable = true,
            Path = "root",
            Children = await this.LoadWorkspaces(),
        });

        this.treeItems.Add(new TreeDivider());
        this.treeItems.Add(new TreeItemData
        {
            Depth = 0,
            Branch = WorkspaceBranch.TEMPORARY_CHATS,
            Text = "Temporary chats",
            Icon = Icons.Material.Filled.Timer,
            Expandable = true,
            Path = "temp",
            Children = await this.LoadTemporaryChats(),
        });
    }

    private async Task<HashSet<ITreeItem>> LoadTemporaryChats()
    {
        var tempChildren = new HashSet<ITreeItem>();

        //
        // Search for workspace folders in the data directory:
        //
                        
        // Get the workspace root directory: 
        var temporaryDirectories = Path.Join(SettingsManager.DataDirectory, "tempChats");
                        
        // Ensure the directory exists:
        Directory.CreateDirectory(temporaryDirectories);
                        
        // Enumerate the workspace directories:
        foreach (var tempChatDirPath in Directory.EnumerateDirectories(temporaryDirectories))
        {
            // Read the `name` file:
            var chatNamePath = Path.Join(tempChatDirPath, "name");
            var chatName = await File.ReadAllTextAsync(chatNamePath, Encoding.UTF8);
                            
            tempChildren.Add(new TreeItemData
            {
                IsChat = true,
                Depth = 1,
                Branch = WorkspaceBranch.TEMPORARY_CHATS,
                Text = chatName,
                Icon = Icons.Material.Filled.Timer,
                Expandable = false,
                Path = tempChatDirPath,
            });
        }
                        
        return tempChildren;
    }
    
    private async Task<string> LoadWorkspaceName(Guid workspaceId)
    {
        if(workspaceId == Guid.Empty)
            return string.Empty;
        
        var workspacePath = Path.Join(SettingsManager.DataDirectory, "workspaces", workspaceId.ToString());
        var workspaceNamePath = Path.Join(workspacePath, "name");
        return await File.ReadAllTextAsync(workspaceNamePath, Encoding.UTF8);
    }
    
    private async Task<HashSet<ITreeItem>> LoadWorkspaces()
    {
        var workspaces = new HashSet<ITreeItem>();
        
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
                                
            workspaces.Add(new TreeItemData
            {
                IsChat = false,
                Depth = 1,
                Branch = WorkspaceBranch.WORKSPACES,
                Text = workspaceName,
                Icon = Icons.Material.Filled.Description,
                Expandable = true,
                Path = workspaceDirPath,
                Children = await this.LoadWorkspaceChats(workspaceDirPath),
            });
        }
                            
        workspaces.Add(new TreeButton(WorkspaceBranch.WORKSPACES, 1, "Add workspace",Icons.Material.Filled.Add, this.AddWorkspace));
        return workspaces;
    }

    private async Task<HashSet<ITreeItem>> LoadWorkspaceChats(string workspacePath)
    {
        var workspaceChats = new HashSet<ITreeItem>();
        
        // Enumerate the workspace directory:
        foreach (var chatPath in Directory.EnumerateDirectories(workspacePath))
        {
            // Read the `name` file:
            var chatNamePath = Path.Join(chatPath, "name");
            var chatName = await File.ReadAllTextAsync(chatNamePath, Encoding.UTF8);
                                
            workspaceChats.Add(new TreeItemData
            {
                IsChat = true,
                Depth = 2,
                Branch = WorkspaceBranch.WORKSPACES,
                Text = chatName,
                Icon = Icons.Material.Filled.Chat,
                Expandable = false,
                Path = chatPath,
            });
        }
                            
        workspaceChats.Add(new TreeButton(WorkspaceBranch.WORKSPACES, 2, "Add chat",Icons.Material.Filled.Add, () => this.AddChat(workspacePath)));
        return workspaceChats;
    }

    public async Task StoreChat(ChatThread chat)
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
        await this.LoadTreeItems();
        this.StateHasChanged();
    }

    private async Task<ChatThread?> LoadChat(string? chatPath, bool switchToChat)
    {
        if(string.IsNullOrWhiteSpace(chatPath))
            return null;

        if(!Directory.Exists(chatPath))
            return null;

        try
        {
            var chatData = await File.ReadAllTextAsync(Path.Join(chatPath, "thread.json"), Encoding.UTF8);
            var chat = JsonSerializer.Deserialize<ChatThread>(chatData, JSON_OPTIONS);
            if (switchToChat)
            {
                this.CurrentChatThread = chat;
                await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
            }
            
            return chat;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return null;
    }

    private async Task DeleteChat(string? chatPath)
    {
        var chat = await this.LoadChat(chatPath, false);
        if (chat is null)
            return;
        
        var workspaceName = await this.LoadWorkspaceName(chat.WorkspaceId);
        var dialogParameters = new DialogParameters
        {
            { "Message", (chat.WorkspaceId == Guid.Empty) switch
                { 
                    true => $"Are you sure you want to delete the temporary chat '{chat.Name}'?",
                    false => $"Are you sure you want to delete the chat '{chat.Name}' in the workspace '{workspaceName}'?",
                }
            },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Chat", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult.Canceled)
            return;
        
        string chatDirectory;
        if (chat.WorkspaceId == Guid.Empty)
            chatDirectory = Path.Join(SettingsManager.DataDirectory, "tempChats", chat.ChatId.ToString());
        else
            chatDirectory = Path.Join(SettingsManager.DataDirectory, "workspaces", chat.WorkspaceId.ToString(), chat.ChatId.ToString());

        Directory.Delete(chatDirectory, true);
        await this.LoadTreeItems();
        
        if(this.CurrentChatThread?.ChatId == chat.ChatId)
        {
            this.CurrentChatThread = null;
            await this.CurrentChatThreadChanged.InvokeAsync(this.CurrentChatThread);
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
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>("Rename Chat", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult.Canceled)
            return;

        chat.Name = (dialogResult.Data as string)!;
        await this.StoreChat(chat);
        await this.LoadTreeItems();
    }

    private async Task AddWorkspace()
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", "Please name your workspace:" },
            { "UserInput", string.Empty },
            { "ConfirmText", "Add workspace" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>("Add Workspace", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult.Canceled)
            return;
        
        var workspaceId = Guid.NewGuid();
        var workspacePath = Path.Join(SettingsManager.DataDirectory, "workspaces", workspaceId.ToString());
        Directory.CreateDirectory(workspacePath);
        
        var workspaceNamePath = Path.Join(workspacePath, "name");
        await File.WriteAllTextAsync(workspaceNamePath, (dialogResult.Data as string)!, Encoding.UTF8);
        
        await this.LoadTreeItems();
    }
    
    private async Task AddChat(string workspacePath)
    {
        
    }
}