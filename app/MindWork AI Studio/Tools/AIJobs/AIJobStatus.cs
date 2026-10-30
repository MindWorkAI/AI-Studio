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

    /// <summary>
    /// The model asked for a tool call which the user has to confirm first, see ToolApprovalRequest.
    /// </summary>
    WAITING_FOR_APPROVAL,
}