namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionToolFunction
{
    public string Name { get; init; } = string.Empty;

    public string Arguments { get; init; } = string.Empty;
}
