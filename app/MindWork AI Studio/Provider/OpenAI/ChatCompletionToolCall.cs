namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionToolCall
{
    public string Id { get; init; } = string.Empty;

    public string Type { get; init; } = "function";

    public ChatCompletionToolFunction Function { get; init; } = new();
}
