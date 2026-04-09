namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionResponseMessage
{
    public string Role { get; init; } = string.Empty;

    public string? Content { get; init; }

    public IList<ChatCompletionToolCall> ToolCalls { get; init; } = [];
}
