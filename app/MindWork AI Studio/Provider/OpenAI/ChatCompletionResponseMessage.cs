using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionResponseMessage
{
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public JsonElement? RawContent { get; init; }

    [JsonIgnore]
    public string? Content => ChatCompletionContent.GetText(this.RawContent);

    public string? ReasoningContent { get; init; }

    public IList<ChatCompletionToolCall>? ToolCalls { get; init; }
}
