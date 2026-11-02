using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The function a tool of a Chat Completions compatible API offers to the model.
/// </summary>
public sealed record ChatCompletionFunctionDeclaration
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public JsonElement Parameters { get; init; }

    public bool Strict { get; init; }
}