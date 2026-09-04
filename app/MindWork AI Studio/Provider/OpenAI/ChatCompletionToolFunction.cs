namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionToolFunction
{
    public string? Name { get; init; }

    public string? Arguments { get; init; }
}
