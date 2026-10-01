using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionToolCall
{
    public string? Id { get; init; }

    public string? Type { get; init; } = "function";

    public ChatCompletionToolFunction? Function { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalMetadata { get; init; } = new Dictionary<string, JsonElement>();
}
