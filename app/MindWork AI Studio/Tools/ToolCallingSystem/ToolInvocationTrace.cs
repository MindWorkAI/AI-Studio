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

    public Dictionary<string, string> Arguments { get; set; } = [];

    [JsonIgnore]
    public string Result { get; set; } = string.Empty;

    [JsonIgnore]
    public JsonNode? JsonResult { get; set; }
}