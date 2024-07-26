namespace AIStudio.Settings.DataModel;

public enum WorkspaceStorageTemporaryMaintenancePolicy
{
    NO_AUTOMATIC_MAINTENANCE,
    
    DELETE_OLDER_THAN_7_DAYS,
    DELETE_OLDER_THAN_30_DAYS,
    DELETE_OLDER_THAN_90_DAYS,
    DELETE_OLDER_THAN_180_DAYS,
    DELETE_OLDER_THAN_365_DAYS,
}