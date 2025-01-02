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
    /// The position of the splitter between the chat and the workspaces.
    /// </summary>
    public double SplitterPosition { get; set; } = 30;
}