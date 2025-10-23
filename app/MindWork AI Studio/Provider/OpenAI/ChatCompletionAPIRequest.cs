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
    IList<Message> Messages,
    bool Stream
)
{
    public ChatCompletionAPIRequest() : this(string.Empty, [], true)
    {
    }
    
    public IDictionary<string, string>? AdditionalApiParameters { get; init; }
}