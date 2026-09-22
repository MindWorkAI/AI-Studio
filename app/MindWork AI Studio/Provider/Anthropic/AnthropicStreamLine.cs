using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// One line of a streamed Anthropic messages call.
/// </summary>
/// <param name="Type">The kind of event this line reports.</param>
/// <param name="Index">Which content block the event belongs to; blocks are correlated by it.</param>
/// <param name="ContentBlock">The block as it opens, for a content block start.</param>
/// <param name="Delta">The piece this event adds, for a content block delta or a message delta.</param>
public readonly record struct AnthropicStreamLine(string? Type, int Index, JsonElement ContentBlock, AnthropicStreamDelta Delta);