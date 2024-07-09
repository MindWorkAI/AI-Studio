using AIStudio.Chat;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

public partial class Workspaces : ComponentBase
{
    [Inject]
    private SettingsManager SettingsManager { get; set; } = null!;
    
    [Parameter]
    public ChatThread? CurrentChatThread { get; set; }

    private readonly HashSet<ITreeItem<string>> initialTreeItems = new();
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        //
        // Notice: In order to get the server-based loading to work, we need to respect the following rules:
        // - We must have initial tree items
        // - Those initial tree items cannot have children
        // - When assigning the tree items to the MudTreeViewItem component, we must set the Value property to the value of the item
        //
        
        this.initialTreeItems.Add(new TreeItemData<string>
        {
            Depth = 0,
            Branch = WorkspaceBranch.WORKSPACES,
            Text = "Workspaces",
            Icon = Icons.Material.Filled.Folder,
            Expandable = true,
            Value = "root",
        });

        this.initialTreeItems.Add(new TreeDivider<string>());
        this.initialTreeItems.Add(new TreeItemData<string>
        {
            Depth = 0,
            Branch = WorkspaceBranch.TEMPORARY_CHATS,
            Text = "Temporary chats",
            Icon = Icons.Material.Filled.Timer,
            Expandable = true,
            Value = "temp",
        });
        
        await base.OnInitializedAsync();
    }

    #endregion

    private Task<HashSet<ITreeItem<string>>> LoadServerData(ITreeItem<string>? parent)
    {
        switch (parent)
        {
            case TreeItemData<string> item:
                switch (item.Branch)
                {
                    case WorkspaceBranch.WORKSPACES:
                        var workspaceChildren = new HashSet<ITreeItem<string>>();

                        if (item.Depth == 0)
                        {
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
                                workspaceChildren.Add(new TreeItemData<string>
                                {
                                    Depth = item.Depth + 1,
                                    Branch = WorkspaceBranch.WORKSPACES,
                                    Text = Path.GetFileName(workspaceDirPath),
                                    Icon = Icons.Material.Filled.Description,
                                    Expandable = true,
                                    Value = workspaceDirPath,
                                });
                            }
                            
                            workspaceChildren.Add(new TreeButton<string>(WorkspaceBranch.WORKSPACES, item.Depth + 1, "Add workspace",Icons.Material.Filled.Add));
                        }
                        
                        else if (item.Depth == 1)
                        {
                            //
                            // Search for workspace chats in the workspace directory:
                            //
                            
                            // Get the workspace directory:
                            var workspaceDirPath = item.Value;
                            
                            if(workspaceDirPath is null)
                                return Task.FromResult(new HashSet<ITreeItem<string>>());
                            
                            // Enumerate the workspace directory:
                            foreach (var chatPath in Directory.EnumerateDirectories(workspaceDirPath))
                            {
                                workspaceChildren.Add(new TreeItemData<string>
                                {
                                    Depth = item.Depth + 1,
                                    Branch = WorkspaceBranch.WORKSPACES,
                                    Text = Path.GetFileNameWithoutExtension(chatPath),
                                    Icon = Icons.Material.Filled.Chat,
                                    Expandable = false,
                                    Value = chatPath,
                                });
                            }
                            
                            workspaceChildren.Add(new TreeButton<string>(WorkspaceBranch.WORKSPACES, item.Depth + 1, "Add chat",Icons.Material.Filled.Add));
                        }

                        return Task.FromResult(workspaceChildren);
                    
                    case WorkspaceBranch.TEMPORARY_CHATS:
                        var tempChildren = new HashSet<ITreeItem<string>>();

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
                            tempChildren.Add(new TreeItemData<string>
                            {
                                Depth = item.Depth + 1,
                                Branch = WorkspaceBranch.TEMPORARY_CHATS,
                                Text = Path.GetFileName(tempChatDirPath),
                                Icon = Icons.Material.Filled.Timer,
                                Expandable = false,
                                Value = tempChatDirPath,
                            });
                        }
                        
                        return Task.FromResult(tempChildren);
                }

                return Task.FromResult(new HashSet<ITreeItem<string>>());
            
            default:
                return Task.FromResult(new HashSet<ITreeItem<string>>());
        }
    }
}