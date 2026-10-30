using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolRuntimeStatus
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ToolRuntimeStatus).Namespace, nameof(ToolRuntimeStatus));

    public bool IsRunning { get; set; }

    public List<string> ToolNames { get; set; } = [];

    /// <summary>
    /// How the running tool got the permission of the user, when it needed one, see ToolApprovalRules.
    /// </summary>
    /// <remarks>
    /// RUN_ALWAYS_IN_THIS_CHAT also for a call which runs without asking, because the user allowed
    /// its tool for the chat before.
    /// </remarks>
    public ToolApprovalDecision Approval { get; set; } = ToolApprovalDecision.NONE;

    public string Message => this.ToolNames.Count switch
    {
        0 => string.Empty,
        1 when this.Approval is ToolApprovalDecision.RUN_ALWAYS_IN_THIS_CHAT => string.Format(TB("Using tool: {0}. You allowed it for this chat."), this.ToolNames[0]),
        1 when this.Approval is ToolApprovalDecision.RUN => string.Format(TB("Using tool: {0}. You allowed this call."), this.ToolNames[0]),
        1 => string.Format(TB("Using tool: {0}"), this.ToolNames[0]),
        _ => string.Format(TB("Using tools: {0}"), string.Join(", ", this.ToolNames)),
    };
}
