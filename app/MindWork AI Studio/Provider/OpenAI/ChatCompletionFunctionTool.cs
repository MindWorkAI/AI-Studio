namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The function tool definition shape expected by Chat Completions compatible APIs, with the function nested.
/// </summary>
public sealed record ChatCompletionFunctionTool
{
    public string Type { get; init; } = "function";

    public ChatCompletionFunctionDeclaration Function { get; init; } = new();
}