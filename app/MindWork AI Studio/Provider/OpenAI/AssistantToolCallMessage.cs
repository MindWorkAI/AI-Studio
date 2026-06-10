namespace AIStudio.Provider.OpenAI;

public sealed record AssistantToolCallMessage : IMessageBase
{
    public string Role { get; init; } = "assistant";

    public string? Content { get; init; }

    public IList<ChatCompletionToolCall> ToolCalls { get; init; } = [];
}
