using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// One line of a streamed Anthropic messages call.
/// </summary>
/// <param name="Type">The kind of event this line reports.</param>
/// <param name="Index">Which content block the event belongs to; blocks are correlated by it.</param>
/// <param name="ContentBlock">The block as it opens, for a content block start.</param>
/// <param name="Delta">The piece this event adds, for a content block delta or a message delta.</param>
public readonly record struct AnthropicStreamLine(string? Type, int Index, JsonElement ContentBlock, AnthropicStreamDelta Delta)
{
    /// <summary>
    /// The message the stream opens with, for a message start.
    /// </summary>
    /// <remarks>
    /// Not a positional parameter, because nobody but the serializer ever builds this line with
    /// one. It is what states the usage, for the reason given at ResponseStreamLine.GetUsage.
    /// </remarks>
    public AnthropicStreamMessage? Message { get; init; }
}