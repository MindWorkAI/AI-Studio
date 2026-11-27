namespace AIStudio.Chat.Compression;

/// <summary>
/// What a chat does with a message which would fill the context window of the model, see ContextWindowFullHandling.Decide.
/// </summary>
public enum ContextWindowFullAction
{
    /// <summary>
    /// Ask the user what to do, see ContextWindowFullDialog.
    /// </summary>
    ASK,

    /// <summary>
    /// Send the message without asking.
    /// </summary>
    SEND,

    /// <summary>
    /// Compress the chat without asking, and decide once more afterward.
    /// </summary>
    COMPRESS,
}