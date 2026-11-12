namespace AIStudio.Dialogs;

/// <summary>
/// What should happen with a message which does not fit into the context window of the model.
/// </summary>
public enum ContextWindowFullDecision
{
    /// <summary>
    /// Compress the chat first. The message goes along into the new chat, where the user sends it.
    /// </summary>
    COMPRESS,

    /// <summary>
    /// Send the message as it is and leave it to the provider what happens.
    /// </summary>
    SEND_ANYWAY,
}