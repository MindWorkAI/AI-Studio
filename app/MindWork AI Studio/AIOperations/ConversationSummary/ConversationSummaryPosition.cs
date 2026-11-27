namespace AIStudio.AIOperations.ConversationSummary;

/// <summary>
/// Where a part of the conversation begins, see ConversationSummaryInput.TakePart.
/// </summary>
/// <param name="Message">The index of the first message of the part.</param>
/// <param name="Offset">Where in the text of that message the part begins: 0, unless the part before split the message.</param>
public readonly record struct ConversationSummaryPosition(int Message, int Offset)
{
    /// <summary>
    /// The beginning of the conversation.
    /// </summary>
    public static readonly ConversationSummaryPosition START = new(0, 0);
}