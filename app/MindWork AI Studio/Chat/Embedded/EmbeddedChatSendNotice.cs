namespace AIStudio.Chat.Embedded;

/// <summary>
/// What an embedded chat tells the user before it sends a message, see EmbeddedChatTokenBudget.NoticeBeforeSending.
/// </summary>
public enum EmbeddedChatSendNotice
{
    /// <summary>
    /// Nothing: the message fits, or nobody knows the context window of the model.
    /// </summary>
    NONE,

    /// <summary>
    /// The message nearly fills the context window; the user may send it anyway.
    /// </summary>
    NEARLY_FULL,

    /// <summary>
    /// With the message, the conversation leaves no room for the next answer; the chat does not send it.
    /// </summary>
    FULL,
}