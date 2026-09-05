namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionResponse
{
    public string Id { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public IList<ChatCompletionResponseChoice> Choices { get; init; } = [];
}
