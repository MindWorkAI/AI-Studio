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
    /// Executes one non-streamed round and returns what the model answered.
    /// </summary>
    /// <param name="finalResponseInstruction">
    /// When set, the instruction telling the model that no more tools are available. The adapter
    /// appends it to the system prompt for this round only.
    /// </param>
    /// <param name="includeTools">Whether the tools may be offered in this round.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>
    /// The round's outcome, or null when the request failed. Null ends the loop without an error
    /// message because the adapter has already told the user what went wrong.
    /// </returns>
    public Task<ToolCallingRound?> ExecuteRoundAsync(string? finalResponseInstruction, bool includeTools, CancellationToken token = default);

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
}