using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The request body for the Responses API.
/// </summary>
/// <param name="Model">Which model to use.</param>
/// <param name="Input">The chat messages and Responses API input items.</param>
/// <param name="Stream">Whether to stream the response.</param>
/// <param name="Store">Whether to store the response on the server (usually OpenAI's infrastructure).</param>
/// <param name="Tools">The local function tools to use for the request.</param>
public record ResponsesAPIRequest(
    string Model,
    IList<object> Input,
    bool Stream,
    bool Store,
    IList<object> Tools)
{
    //
    // The properties of the parameters, declared again to be settable. The source generation of System.Text.Json
    // sets every init-only property while it creates the object, also one missing in the JSON, so its value from
    // the parameterless constructor would get lost. Keep them first and in the order of the parameters, which is
    // the order they get written in:
    //
    public string Model { get; set; } = Model;
    public IList<object> Input { get; set; } = Input;
    public bool Stream { get; set; } = Stream;
    public bool Store { get; set; } = Store;
    public IList<object> Tools { get; set; } = Tools;

    public ResponsesAPIRequest() : this(string.Empty, [], true, false, [])
    {
    }

    //
    // Attention: The "required" modifier is not supported for [JsonExtensionData], and neither is init: the source
    // generation of System.Text.Json refuses to write the request at all then.
    //
    [JsonExtensionData]
    public IDictionary<string, object> AdditionalApiParameters { get; set; } = new Dictionary<string, object>();
}
