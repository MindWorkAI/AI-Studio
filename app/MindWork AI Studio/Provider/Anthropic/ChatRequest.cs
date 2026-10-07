using System.Text.Json.Serialization;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// The Anthropic chat request model.
/// </summary>
/// <param name="Model">Which model to use for chat completion.</param>
/// <param name="Messages">The chat messages.</param>
/// <param name="MaxTokens">The maximum number of tokens to generate.</param>
/// <param name="Stream">Whether to stream the chat completion.</param>
/// <param name="System">The system prompt for the chat completion.</param>
public readonly record struct ChatRequest(
    string Model,
    IList<IMessageBase> Messages,
    int MaxTokens,
    bool Stream,
    string System
)
{
    /// <summary>
    /// The tools the model may call, or null when it should answer without them.
    /// </summary>
    /// <remarks>
    /// Omitted from the request when null: sending an empty list is not the same as sending no
    /// tools at all, and the final round of a tool conversation has to offer none.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<AnthropicTool>? Tools { get; init; }

    // Attention: The "required" modifier is not supported for [JsonExtensionData].
    [JsonExtensionData]
    public IDictionary<string, object> AdditionalApiParameters { get; init; } = new Dictionary<string, object>();
}
