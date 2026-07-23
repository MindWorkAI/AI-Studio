using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The delta text of a choice.
/// </summary>
public sealed record ChatCompletionDelta
{
    [JsonPropertyName("content")]
    public JsonElement? RawContent { get; init; }

    [JsonIgnore]
    public string Content => ChatCompletionContent.GetText(this.RawContent) ?? string.Empty;
}
