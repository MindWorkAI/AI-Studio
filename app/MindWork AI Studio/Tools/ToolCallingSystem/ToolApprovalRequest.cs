namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// A tool call which waits until the user confirms or declines it.
/// </summary>
/// <remarks>
/// The tool calling loop puts it on the assistant message, see ContentText.PendingToolApproval, and
/// waits for the answer; the message block shows it and passes on what the user clicked. Never
/// stored: after a restart, the request it belongs to is gone as well.
/// </remarks>
public sealed class ToolApprovalRequest
{
    private readonly TaskCompletionSource<ToolApprovalDecision> decision = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public required string ToolName { get; init; }

    public required string ToolIcon { get; init; }

    /// <summary>
    /// The arguments of the call as the trace shows them, with sensitive ones masked.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Arguments { get; init; }

    /// <summary>
    /// Whether the user may allow the tool for the rest of the chat, see ToolApprovalRules.MayApproveForWholeChat.
    /// </summary>
    public required bool MayApproveForWholeChat { get; init; }

    /// <summary>
    /// Whether the user answered already.
    /// </summary>
    public bool IsAnswered => this.decision.Task.IsCompleted;

    /// <summary>
    /// Passes on what the user decided. Only the first answer counts.
    /// </summary>
    /// <param name="answer">The decision of the user.</param>
    public void Answer(ToolApprovalDecision answer)
    {
        // An allowance the organization does not permit counts as a single confirmation:
        if (answer is ToolApprovalDecision.RUN_ALWAYS_IN_THIS_CHAT && !this.MayApproveForWholeChat)
            answer = ToolApprovalDecision.RUN;

        this.decision.TrySetResult(answer);
    }

    /// <summary>
    /// Waits for the decision of the user.
    /// </summary>
    /// <param name="token">Ends the wait, e.g., when the user stops the request.</param>
    /// <returns>The decision.</returns>
    public Task<ToolApprovalDecision> WaitAsync(CancellationToken token) => this.decision.Task.WaitAsync(token);
}