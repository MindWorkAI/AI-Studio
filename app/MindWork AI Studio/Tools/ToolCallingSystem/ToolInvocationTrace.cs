using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolInvocationTrace
{
    public int Order { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string ToolIcon { get; set; } = AppIcons.TOOL;

    public string ToolCallId { get; set; } = string.Empty;

    public ToolInvocationTraceStatus Status { get; set; } = ToolInvocationTraceStatus.NONE;

    public bool WasExecuted { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public Dictionary<string, string> Arguments { get; set; } = [];

    /// <summary>
    /// The arguments as the model passed them, as one JSON object, but without the values of the
    /// arguments the tool marked as sensitive.
    /// </summary>
    /// <remarks>
    /// Arguments holds the values as text for the user to read, which loses their JSON type: a
    /// boolean reads "True", and a query "2026" reads like a number. The note which tells the model
    /// in a later message how it called the tool needs the values as the model passed them, see
    /// ToolCallHistoryNote. A name which Arguments holds but this object does not was sensitive. A
    /// trace from before this property existed holds an empty text, so all of its arguments count
    /// as such: the note then hides their values instead of guessing their types.
    /// </remarks>
    public string ArgumentsJson { get; set; } = string.Empty;

    [JsonIgnore]
    public string Result { get; set; } = string.Empty;

    [JsonIgnore]
    public JsonNode? JsonResult { get; set; }
}