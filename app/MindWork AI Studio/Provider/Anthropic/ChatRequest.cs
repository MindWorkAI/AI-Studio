using System.Text.Json.Serialization;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// The Anthropic chat request model.
/// </summary>
/// <param name="Model">Which model to use for chat completion.</param>
/// <param name="Messages">The chat messages.</param>
/// <param name="MaxTokens">The maximum number of tokens to generate.</param>
/// <param name="Stream">Whether to stream the chat completion.</param>
public readonly record struct ChatRequest(
    string Model,
    IList<IMessageBase> Messages,
    int MaxTokens,
    bool Stream)
{
    // Attention: The "required" modifier is not supported for [JsonExtensionData].
    [JsonExtensionData]
    public IDictionary<string, object> AdditionalApiParameters { get; init; } = new Dictionary<string, object>();
}