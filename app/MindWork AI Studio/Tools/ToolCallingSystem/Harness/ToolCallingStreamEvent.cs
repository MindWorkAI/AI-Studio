using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem.Harness;

/// <summary>
/// One event of a streamed round of a tool calling conversation.
/// </summary>
/// <remarks>
/// Text arrives while the round is still running, its outcome only at the end. A round which ends
/// without a ROUND_COMPLETED event has failed: that is how a failed request or a truncated stream
/// is told apart from a round which simply had nothing to say. The adapter has already told the
/// user what went wrong in that case, so the loop ends without a message of its own.
/// </remarks>
/// <param name="Kind">What this event carries.</param>
/// <param name="Delta">The piece of text, set for TEXT_DELTA events only.</param>
/// <param name="Round">The round's outcome, set for ROUND_COMPLETED events only.</param>
public sealed record ToolCallingStreamEvent(ToolCallingStreamEventKind Kind, ContentStreamChunk? Delta, ToolCallingRound? Round)
{
    /// <summary>
    /// Creates an event for a piece of text, along with the sources it brought.
    /// </summary>
    /// <param name="delta">The chunk to show.</param>
    public static ToolCallingStreamEvent TextDelta(ContentStreamChunk delta) => new(ToolCallingStreamEventKind.TEXT_DELTA, delta, null);
    
    /// <summary>
    /// Creates an event for a piece of text without any sources.
    /// </summary>
    /// <param name="text">The text to show.</param>
    public static ToolCallingStreamEvent TextDelta(string text) => new(ToolCallingStreamEventKind.TEXT_DELTA, new ContentStreamChunk(text, []), null);
    
    /// <summary>
    /// Creates the event which ends a round.
    /// </summary>
    /// <param name="round">The round's outcome.</param>
    public static ToolCallingStreamEvent RoundCompleted(ToolCallingRound round) => new(ToolCallingStreamEventKind.ROUND_COMPLETED, null, round);
}