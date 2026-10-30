namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// What the user answered when a tool call waited for confirmation.
/// </summary>
public enum ToolApprovalDecision
{
    NONE = 0,

    /// <summary>
    /// Run this one call.
    /// </summary>
    RUN,

    /// <summary>
    /// Run this call, and every later call of the same tool collection in this chat without asking.
    /// </summary>
    RUN_ALWAYS_IN_THIS_CHAT,

    /// <summary>
    /// Do not run the call; the model learns that the user declined it.
    /// </summary>
    DECLINE,
}