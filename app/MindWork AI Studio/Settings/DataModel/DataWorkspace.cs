using System.Text.Json.Serialization;

namespace AIStudio.Settings.DataModel;

public sealed class DataWorkspace
{
    /// <summary>
    /// How the chats are stored: automatically, only when the user saves them, or not at all.
    /// </summary>
    /// <remarks>
    /// The chat settings offer it, although it lives here: it keeps its place and the names of its
    /// values, so that older versions of AI Studio still read the settings file. Whether chats are
    /// stored and whether workspaces are used are two questions of their own, though, see
    /// <see cref="AreChatsStored"/> and <see cref="UsesWorkspaces"/>.
    /// </remarks>
    public WorkspaceStorageBehavior StorageBehavior { get; set; } = WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY;

    /// <summary>
    /// When disappearing chats are deleted, that is, the chats outside of a workspace.
    /// </summary>
    public WorkspaceStorageTemporaryMaintenancePolicy StorageTemporaryMaintenancePolicy { get; set; } = WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_90_DAYS;

    /// <summary>
    /// Whether the user switched workspaces on or off, or null while the user never did.
    /// </summary>
    /// <remarks>
    /// Null in every settings file written before workspaces had a switch of their own. Workspaces then
    /// follow the storing of chats, as they did before: whoever chose to store no chat had switched
    /// workspaces off with it, and must not find them back. Ask <see cref="UsesWorkspaces"/>.
    /// </remarks>
    public bool? AreWorkspacesEnabled { get; set; }

    /// <summary>
    /// Whether the user uses workspaces, whatever happens to new chats.
    /// </summary>
    /// <remarks>
    /// Switched off, the sidebar lists the disappearing chats only, while the chats in workspaces stay
    /// where they are and come back once the user switches workspaces on again. Switched on while no
    /// chat is stored, the workspaces keep what they hold, but no new chat goes into one, see
    /// <see cref="CanChatsGoIntoWorkspaces"/>.
    /// </remarks>
    [JsonIgnore]
    public bool UsesWorkspaces => this.AreWorkspacesEnabled ?? this.AreChatsStored;

    /// <summary>
    /// Whether chats are stored at all.
    /// </summary>
    /// <remarks>
    /// Otherwise, a chat lasts only while AI Studio keeps it in memory, which is a matter of privacy.
    /// </remarks>
    [JsonIgnore]
    public bool AreChatsStored => this.StorageBehavior is not WorkspaceStorageBehavior.DISABLE_WORKSPACES;

    /// <summary>
    /// Whether a chat which may not be stored yet can go into a workspace: the open chat, or a chat a
    /// template starts. Putting it there stores it, so this needs both workspaces and stored chats.
    /// </summary>
    [JsonIgnore]
    public bool CanChatsGoIntoWorkspaces => this.AreChatsStored && this.UsesWorkspaces;

    /// <summary>
    /// Whether the sidebar has something to list: the chats AI Studio stores, or the workspaces.
    /// </summary>
    [JsonIgnore]
    public bool IsChatListShown => this.AreChatsStored || this.UsesWorkspaces;

    /// <summary>
    /// The behavior used for displaying the workspace.
    /// </summary>
    public WorkspaceDisplayBehavior DisplayBehavior { get; set; } = WorkspaceDisplayBehavior.TOGGLE_SIDEBAR;

    /// <summary>
    /// Indicates whether the sidebar is currently visible.
    /// </summary>
    public bool IsSidebarVisible { get; set; } = true;

    /// <summary>
    /// Whether the section with the workspaces is collapsed in the sidebar.
    /// </summary>
    public bool IsWorkspacesSectionCollapsed { get; set; }

    /// <summary>
    /// Whether the section with the disappearing chats is collapsed in the sidebar.
    /// </summary>
    public bool IsTemporaryChatsSectionCollapsed { get; set; }

    /// <summary>
    /// The width of the workspace sidebar beside the chat, in CSS pixels, as the user dragged it.
    /// </summary>
    /// <remarks>
    /// The chat page keeps the sidebar within limits whatever the stored width, see Chat.razor.css.
    /// </remarks>
    public double SidebarWidth { get; set; } = 288;
}