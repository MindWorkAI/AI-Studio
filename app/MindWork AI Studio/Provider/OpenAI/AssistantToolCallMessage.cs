using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

public sealed record AssistantToolCallMessage : IMessageBase
{
    public string Role { get; init; } = "assistant";

    public JsonElement? Content { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningContent { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reasoning { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<JsonElement>? ReasoningDetails { get; init; }

    public IList<ChatCompletionToolCall> ToolCalls { get; init; } = [];
}
