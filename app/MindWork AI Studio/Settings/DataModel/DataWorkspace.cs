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
}