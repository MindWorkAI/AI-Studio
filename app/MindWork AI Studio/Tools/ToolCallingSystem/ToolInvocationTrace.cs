using System.Text.Json;
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

    /// <summary>
    /// The arguments this trace kept with their values, see ArgumentsJson.
    /// </summary>
    /// <remarks>
    /// AI Studio writes them itself, see ToolExecutor. Text which is no JSON object, as in a chat file
    /// changed by hand, counts as nothing kept, so the values of all arguments stay unknown.
    /// </remarks>
    /// <returns>The arguments as one JSON object, which the caller disposes, or null when none were kept.</returns>
    public JsonDocument? ReadKeptArguments()
    {
        if (string.IsNullOrWhiteSpace(this.ArgumentsJson))
            return null;

        try
        {
            var document = JsonDocument.Parse(this.ArgumentsJson);
            if (document.RootElement.ValueKind is JsonValueKind.Object)
                return document;

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The texts among the kept arguments, also those inside an array or an object.
    /// </summary>
    /// <returns>The texts, which leave out the values of sensitive arguments.</returns>
    public List<string> FindKeptTexts()
    {
        var texts = new List<string>();
        using var keptArguments = this.ReadKeptArguments();
        if (keptArguments is not null)
            ToolExecutor.CollectStrings(keptArguments.RootElement, texts);

        return texts;
    }
}