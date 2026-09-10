using System.Text.Json;

using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider.Fireworks;

/// <summary>
/// The delta text of a choice.
/// </summary>
/// <param name="Content">The content of the delta text.</param>
/// <param name="ReasoningContent">OpenAI-compatible reasoning content.</param>
/// <param name="Reasoning">OpenRouter-compatible reasoning content.</param>
/// <param name="ReasoningDetails">Structured reasoning details.</param>
public readonly record struct Delta(string Content, string? ReasoningContent, string? Reasoning, IList<JsonElement>? ReasoningDetails)
{
    public string Thinking => ThinkingContent.Get(this.ReasoningContent, this.Reasoning, this.ReasoningDetails);
}
