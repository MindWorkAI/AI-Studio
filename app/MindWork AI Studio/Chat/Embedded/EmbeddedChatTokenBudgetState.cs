namespace AIStudio.Chat.Embedded;

/// <summary>
/// How much room an embedded chat has left in the context of its model.
/// </summary>
public enum EmbeddedChatTokenBudgetState
{
    /// <summary>
    /// Nobody knows: the count failed, or the model has no statement about its context.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// Room enough.
    /// </summary>
    OK,

    /// <summary>
    /// Most of the context is used; the user should think about starting over.
    /// </summary>
    WARNING,

    /// <summary>
    /// The next message and its answer would not fit anymore, so the chat sends no more.
    /// </summary>
    FULL,
}