namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// What became of a tool call which may need the confirmation of the user, see ToolApprovalRules.
/// </summary>
/// <param name="Decision">How the user allowed the call; NONE when it needed no confirmation, or when it may not run.</param>
/// <param name="DeclinedMessage">What the model reads instead of a result when the call may not run, otherwise null.</param>
public readonly record struct ToolCallApproval(ToolApprovalDecision Decision, string? DeclinedMessage)
{
    /// <summary>
    /// A call which needs no confirmation.
    /// </summary>
    public static readonly ToolCallApproval NOT_NEEDED = new(ToolApprovalDecision.NONE, null);

    /// <summary>
    /// A call the user declined.
    /// </summary>
    public static readonly ToolCallApproval DECLINED_BY_USER = new(ToolApprovalDecision.DECLINE, ToolApprovalRules.DECLINED_BY_USER);

    /// <summary>
    /// A call which needs confirmation where nobody can give it. Nobody decided, hence NONE.
    /// </summary>
    public static readonly ToolCallApproval NOBODY_TO_CONFIRM = new(ToolApprovalDecision.NONE, ToolApprovalRules.NOBODY_TO_CONFIRM);

    /// <summary>
    /// A call the user allowed.
    /// </summary>
    /// <param name="decision">How the user allowed it.</param>
    public static ToolCallApproval Allowed(ToolApprovalDecision decision) => new(decision, null);
}