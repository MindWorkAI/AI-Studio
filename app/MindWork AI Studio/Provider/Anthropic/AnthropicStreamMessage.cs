// ReSharper disable ClassNeverInstantiated.Global
namespace AIStudio.Provider.Anthropic;

/// <summary>
/// The message a streamed Anthropic messages call opens with, as far as it is read.
/// </summary>
/// <remarks>
/// It arrives on the message start event, before any content, and states what the request
/// carried. Its content is always empty there -- the blocks follow as events of their own -- so
/// the usage is all there is to read.
/// </remarks>
public sealed record AnthropicStreamMessage
{
    /// <summary>
    /// What the request carried, where the stream states it.
    /// </summary>
    public AnthropicUsage? Usage { get; init; }
}