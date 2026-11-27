namespace AIStudio.AIOperations.ConversationSummary;

/// <summary>
/// The summary the AI wrote of a chat, or why there is none.
/// </summary>
/// <param name="Success">Whether there is a summary.</param>
/// <param name="Summary">The summary, as the AI wrote it. Not filtered for prompt injections yet.</param>
/// <param name="Issue">Why there is no summary, for the user. Empty on success.</param>
public sealed record ConversationSummaryResult(bool Success, string Summary, string Issue)
{
    public static ConversationSummaryResult Succeeded(string summary) => new(true, summary, string.Empty);

    public static ConversationSummaryResult Failed(string issue) => new(false, string.Empty, issue);
}