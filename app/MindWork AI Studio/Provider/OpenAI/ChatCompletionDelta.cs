using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The delta text of a choice.
/// </summary>
public sealed record ChatCompletionDelta
{
    [JsonPropertyName("content")]
    public JsonElement? RawContent { get; set; }

    [JsonIgnore]
    public string Content => ChatCompletionContent.GetText(this.RawContent) ?? string.Empty;

    /// <summary>
    /// The reasoning text some providers stream next to the answer.
    /// </summary>
    /// <remarks>
    /// Read only to tell that the model is reasoning, see ContentStreamChunk.IsReasoning.
    /// </remarks>
    public string? ReasoningContent { get; set; }
}
