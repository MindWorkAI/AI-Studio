namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// Describes the lifecycle state of an assistant session.
/// </summary>
public enum AssistantSessionStatus
{
    /// <summary>
    /// No session state is available.
    /// </summary>
    NONE,

    /// <summary>
    /// The assistant session is running.
    /// </summary>
    RUNNING,

    /// <summary>
    /// Cancellation was requested and the assistant is shutting down.
    /// </summary>
    CANCELING,

    /// <summary>
    /// The assistant session completed successfully.
    /// </summary>
    COMPLETED,

    /// <summary>
    /// The assistant session was canceled.
    /// </summary>
    CANCELED,

    /// <summary>
    /// The assistant session failed.
    /// </summary>
    FAILED,
}