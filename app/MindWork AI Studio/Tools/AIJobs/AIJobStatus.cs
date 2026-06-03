namespace AIStudio.Tools.AIJobs;

public enum AIJobStatus
{
    NONE,
    QUEUED,
    WAITING_FOR_REMOTE,
    RUNNING,
    COMPLETED,
    CANCELED,
    FAILED,
}