using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolInvocationTrace
{
    public int Order { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string ToolIcon { get; set; } = Icons.Material.Filled.Build;

    public string ToolCallId { get; set; } = string.Empty;

    public ToolInvocationTraceStatus Status { get; set; } = ToolInvocationTraceStatus.NONE;

    public bool WasExecuted { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// What the user decided about this call, see ToolApprovalRules.
    /// </summary>
    /// <remarks>
    /// Stored with the answer, so the answer keeps saying what the user allowed or declined.
    /// RUN_ALWAYS_IN_THIS_CHAT also for a call which ran without asking, because the user allowed
    /// its tool for the chat before. NONE for a call which needed no confirmation, and for a
    /// DECLINED call nobody could confirm.
    /// </remarks>
    public ToolApprovalDecision Approval { get; set; } = ToolApprovalDecision.NONE;

    public Dictionary<string, string> Arguments { get; set; } = [];

    [JsonIgnore]
    public string Result { get; set; } = string.Empty;

    [JsonIgnore]
    public JsonNode? JsonResult { get; set; }
}