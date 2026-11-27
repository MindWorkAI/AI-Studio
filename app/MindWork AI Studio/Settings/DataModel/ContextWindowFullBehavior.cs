namespace AIStudio.Settings.DataModel;

/// <summary>
/// What AI Studio does when a message would fill the context window of the model.
/// </summary>
/// <remarks>
/// It applies where a chat asks before sending: the chat, the embedded chat of the BeyondFacts Lab, and the
/// assistants. An assistant cannot compress its request, so both ways to compress ask there instead. The
/// embedded chat always replaces its history when it compresses, since it has no other chat to continue in.
/// </remarks>
public enum ContextWindowFullBehavior
{
    /// <summary>
    /// Ask what to do: compress the chat, send the message anyway, or cancel.
    /// </summary>
    ASK = 0,

    /// <summary>
    /// Compress the chat without asking and continue in a new chat; the original chat stays as it is.
    /// </summary>
    COMPRESS_INTO_NEW_CHAT,

    /// <summary>
    /// Compress the chat without asking and replace its history with the summary; the original messages are gone.
    /// </summary>
    COMPRESS_AND_REPLACE,

    /// <summary>
    /// Send the message without asking; the provider may refuse it when it does not fit.
    /// </summary>
    SEND_WITHOUT_ASKING,
}