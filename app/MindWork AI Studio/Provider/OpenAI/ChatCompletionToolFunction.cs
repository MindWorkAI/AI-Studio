namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionToolFunction
{
    public string? Name { get; set; }

    public string? Arguments { get; set; }
}
