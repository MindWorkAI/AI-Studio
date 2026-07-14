namespace AIStudio.Tools.Services;

/// <summary>Lifecycle status retained independently for each owner.</summary>
public enum MediaImportStatus
{
    QUEUED,
    RUNNING,
    CANCELING,
    SUCCEEDED,
    FAILED,
    CANCELLED,
}