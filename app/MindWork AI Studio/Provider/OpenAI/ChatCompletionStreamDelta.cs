using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// What one choice of a streamed Chat Completions answer adds in this line.
/// </summary>
/// <remarks>
/// This is the delta of the plain text path plus the two fields that path has no use for: the
/// reasoning some providers send alongside, and the tool calls the model asks for.
/// </remarks>
public sealed record ChatCompletionStreamDelta
{
    /// <summary>
    /// The content as it arrived: a string for most providers, a list of parts for some.
    /// </summary>
    [JsonPropertyName("content")]
    public JsonElement? RawContent { get; init; }

    /// <summary>
    /// The text of this fragment, whichever shape it arrived in.
    /// </summary>
    [JsonIgnore]
    public string Content => ChatCompletionContent.GetText(this.RawContent) ?? string.Empty;

    /// <summary>
    /// The reasoning text some providers stream next to the answer.
    /// </summary>
    public string? ReasoningContent { get; init; }

    /// <summary>
    /// The fragments of the tool calls the model is asking for.
    /// </summary>
    public IList<ChatCompletionToolCallDelta?>? ToolCalls { get; init; }
}