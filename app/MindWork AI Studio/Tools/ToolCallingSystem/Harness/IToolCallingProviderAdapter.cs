namespace AIStudio.Tools.ToolCallingSystem.Harness;

/// <summary>
/// Translates between the tool calling loop and one provider API's request and response shapes.
/// </summary>
/// <remarks>
/// The loop itself is the same for every provider: ask, execute what was asked for, ask again.
/// What differs is the wire format — Chat Completions puts tool calls in a message and takes
/// results as tool messages, the Responses API uses function call items correlated by call ID,
/// and Anthropic uses content blocks. An adapter hides exactly that difference.<br/><br/>
/// An adapter is stateful and belongs to one streaming call: it accumulates the conversation
/// the next round has to see. Do not share one across calls.
/// </remarks>
public interface IToolCallingProviderAdapter
{
    /// <summary>
    /// Executes one round and streams what the model answers.
    /// </summary>
    /// <remarks>
    /// Every piece of text the model writes travels as a TEXT_DELTA event, including the text it
    /// writes before it calls a tool. The round's outcome carries that text as well, but only so
    /// that the loop can tell an answered round from a silent one -- whatever reaches the user
    /// reaches them through the deltas, and through them only.
    /// </remarks>
    /// <param name="finalResponseInstruction">
    /// When set, the instruction telling the model that no more tools are available. The adapter
    /// appends it to the system prompt for this round only.
    /// </param>
    /// <param name="includeTools">Whether the tools may be offered in this round.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>
    /// The events of this round: any number of TEXT_DELTA events, closed by one ROUND_COMPLETED
    /// event carrying the outcome. A stream which ends without that closing event is a failed
    /// round; it ends the loop without an error message because the adapter has already told the
    /// user what went wrong.
    /// </returns>
    public IAsyncEnumerable<ToolCallingStreamEvent> ExecuteRoundAsync(string? finalResponseInstruction, bool includeTools, CancellationToken token = default);

    /// <summary>
    /// Records the model's turn from the round just executed, so that the next round sees it.
    /// </summary>
    /// <remarks>
    /// Called before any tool result of that round is recorded. What exactly has to be kept is
    /// the adapter's business: Chat Completions needs the assistant message with its tool calls,
    /// while the Responses API needs every output item, including reasoning items, or it refuses
    /// to continue.
    /// </remarks>
    public void RecordAssistantTurn();

    /// <summary>
    /// Records the result of one tool call so that the next round sees it.
    /// </summary>
    /// <param name="callId">The ID of the call this result belongs to.</param>
    /// <param name="content">The result as the model should see it.</param>
    /// <param name="isError">
    /// Whether the tool failed instead of returning a result. Only some APIs can express this;
    /// the others carry the failure in the content, which is where it has to be legible anyway.
    /// </param>
    public void RecordToolResult(string callId, string content, bool isError = false);

    /// <summary>
    /// The texts which everything recorded so far adds to the request of every following round.
    /// </summary>
    /// <remarks>
    /// Kept by the adapter rather than by the loop, because the adapter is the only place which
    /// knows what actually travels. The loop hands over arguments and results and would count
    /// those; what the Responses API additionally demands back -- its reasoning items -- never
    /// passes through the loop at all, and a conversation whose largest part is invisible is the
    /// very thing this is here to rule out.<br/><br/>
    /// These texts exist for as long as the adapter does, which is one streaming call. Nothing of
    /// this reaches the next request the user sends: the accumulated conversation goes away with
    /// the adapter.
    /// </remarks>
    public IReadOnlyList<string> RecordedRequestTexts { get; }
}