namespace AIStudio.AIOperations.ConversationSummary;

/// <summary>
/// One part of the conversation which the AI summarizes in one request, see ConversationSummaryInput.TakePart.
/// </summary>
/// <param name="Text">The messages of the part, rendered for the summary, with markers where a message breaks off or goes on.</param>
/// <param name="Next">Where the next part begins; its message index equals the number of messages when no part follows.</param>
public readonly record struct ConversationSummaryPart(string Text, ConversationSummaryPosition Next);