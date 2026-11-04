namespace AIStudio.Settings.DataModel;

public sealed class DataWorkspace
{
    /// <summary>
    /// The chat storage behavior.
    /// </summary>
    public WorkspaceStorageBehavior StorageBehavior { get; set; } = WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY;
    
    /// <summary>
    /// The chat storage maintenance behavior.
    /// </summary>
    public WorkspaceStorageTemporaryMaintenancePolicy StorageTemporaryMaintenancePolicy { get; set; } = WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_90_DAYS;

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