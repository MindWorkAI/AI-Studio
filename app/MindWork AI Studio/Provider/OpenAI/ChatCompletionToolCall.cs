using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

public sealed record ChatCompletionToolCall
{
    public string? Id { get; set; }

    public string? Type { get; set; } = "function";

    public ChatCompletionToolFunction? Function { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalMetadata { get; set; } = new Dictionary<string, JsonElement>();
}
