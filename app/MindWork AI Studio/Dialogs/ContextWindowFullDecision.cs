namespace AIStudio.Dialogs;

/// <summary>
/// What should happen with a message which does not fit into the context window of the model.
/// </summary>
public enum ContextWindowFullDecision
{
    /// <summary>
    /// Send nothing. The message stays where it is, so the user can change it.
    /// </summary>
    CANCEL,

    /// <summary>
    /// Compress the chat first. The message goes along, and the user sends it afterward.
    /// </summary>
    COMPRESS,

    /// <summary>
    /// Send the message as it is and leave it to the provider what happens.
    /// </summary>
    SEND_ANYWAY,
}