using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The OpenAI's legacy chat completion request model.
/// </summary>
/// <param name="Model">Which model to use for chat completion.</param>
/// <param name="Messages">The chat messages.</param>
/// <param name="Stream">Whether to stream the chat completion.</param>
public record ChatCompletionAPIRequest(
    string Model,
    IList<IMessageBase> Messages,
    bool Stream
)
{
    //
    // The properties of the parameters, declared again to be settable. The source generation of System.Text.Json
    // sets every init-only property while it creates the object, also one missing in the JSON, so its value from
    // the parameterless constructor would get lost. Keep them first and in the order of the parameters, which is
    // the order they get written in:
    //
    public string Model { get; set; } = Model;
    public IList<IMessageBase> Messages { get; set; } = Messages;
    public bool Stream { get; set; } = Stream;

    public ChatCompletionAPIRequest() : this(string.Empty, [], true)
    {
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<object>? Tools { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ParallelToolCalls { get; set; }

    /// <summary>
    /// Asks a streamed request to end with what it cost.
    /// </summary>
    /// <remarks>
    /// Derived rather than set, so that every provider which builds one of these asks for it
    /// without having to know that it exists. A request which is not streamed carries no such
    /// line, and then the block would only be a field the provider has to ignore.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatCompletionStreamOptions? StreamOptions => this.Stream ? ChatCompletionStreamOptions.INCLUDE_USAGE : null;
    
    //
    // Attention: The "required" modifier is not supported for [JsonExtensionData], and neither is init: the source
    // generation of System.Text.Json refuses to write the request at all then.
    //
    [JsonExtensionData]
    public IDictionary<string, object> AdditionalApiParameters { get; set; } = new Dictionary<string, object>();
}
