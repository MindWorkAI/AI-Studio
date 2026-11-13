using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

public sealed record AssistantToolCallMessage : IMessageBase
{
    public string Role { get; set; } = "assistant";

    public JsonElement? Content { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningContent { get; set; }

    public IList<ChatCompletionToolCall> ToolCalls { get; set; } = [];
}
