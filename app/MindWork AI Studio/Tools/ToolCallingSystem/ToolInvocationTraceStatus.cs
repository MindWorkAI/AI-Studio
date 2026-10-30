namespace AIStudio.Tools.ToolCallingSystem;

public enum ToolInvocationTraceStatus
{
    NONE = 0,
    SUCCESS,
    ERROR,
    BLOCKED,

    /// <summary>
    /// The user declined the call, or nobody was there to confirm it, so it never ran.
    /// </summary>
    DECLINED,
}