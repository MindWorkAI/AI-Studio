using System.Text.Json.Serialization;

namespace AIStudio.Provider.Mistral;

/// <summary>
/// The OpenAI chat request model.
/// </summary>
/// <param name="Model">Which model to use for chat completion.</param>
/// <param name="Messages">The chat messages.</param>
/// <param name="Stream">Whether to stream the chat completion.</param>
/// <param name="RandomSeed">The seed for the chat completion.</param>
/// <param name="SafePrompt">Whether to inject a safety prompt before all conversations.</param>
public readonly record struct ChatRequest(
    string Model,
    IList<IMessageBase> Messages,
    bool Stream,
    int RandomSeed,
    bool SafePrompt = false
)
{
    // Attention: The "required" modifier is not supported for [JsonExtensionData].
    [JsonExtensionData]
    public IDictionary<string, object> AdditionalApiParameters { get; init; } = new Dictionary<string, object>();
}