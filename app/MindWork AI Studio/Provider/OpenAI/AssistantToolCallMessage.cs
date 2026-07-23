using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

public sealed record AssistantToolCallMessage : IMessageBase
{
    public string Role { get; init; } = "assistant";

    public string? Content { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningContent { get; init; }

    public IList<ChatCompletionToolCall> ToolCalls { get; init; } = [];
}
