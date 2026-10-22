namespace AIStudio.Provider.Anthropic;

/// <summary>
/// One piece of a streamed content block.
/// </summary>
/// <remarks>
/// Which of the fields is set depends on what the block is made of: text arrives as text, tool
/// arguments as fragments of JSON, and a thinking block brings its signature in one piece at the
/// end. The stop reason belongs to the message rather than to a block, and shares this shape
/// because the API sends it in a delta of its own.
/// </remarks>
/// <param name="Type">What kind of piece this is.</param>
/// <param name="Text">The piece of text, for a text delta.</param>
/// <param name="PartialJson">The fragment of the tool arguments, for an input JSON delta.</param>
/// <param name="Thinking">The piece of thinking, for a thinking delta.</param>
/// <param name="Signature">The signature of a thinking block, for a signature delta.</param>
/// <param name="StopReason">Why the model stopped, for the message delta.</param>
public readonly record struct AnthropicStreamDelta(string? Type, string? Text, string? PartialJson, string? Thinking, string? Signature, string? StopReason);