using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The request body for the Responses API.
/// </summary>
/// <param name="Model">Which model to use.</param>
/// <param name="Input">The chat messages.</param>
/// <param name="Stream">Whether to stream the response.</param>
/// <param name="Store">Whether to store the response on the server (usually OpenAI's infrastructure).</param>
/// <param name="Tools">The tools to use for the request.</param>
public record ResponsesAPIRequest(
    string Model,
    IList<Message> Input,
    bool Stream,
    bool Store,
    IList<Tool> Tools)
{
    public ResponsesAPIRequest() : this(string.Empty, [], true, false, [])
    {
    }
    
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalApiParameters { get; init; }
}