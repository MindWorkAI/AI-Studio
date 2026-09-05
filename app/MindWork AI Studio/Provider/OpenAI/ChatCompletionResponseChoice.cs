namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionResponseChoice
{
    public int Index { get; init; }

    public string FinishReason { get; init; } = string.Empty;

    public ChatCompletionResponseMessage Message { get; init; } = new();
}
