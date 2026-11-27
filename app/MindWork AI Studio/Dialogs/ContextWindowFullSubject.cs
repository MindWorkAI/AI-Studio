namespace AIStudio.Dialogs;

/// <summary>
/// What is about to be sent when the context window of the model is nearly full, see ContextWindowFullDialog.
/// </summary>
/// <remarks>
/// Decides how the notice words what fills the window and what compressing would do.
/// </remarks>
public enum ContextWindowFullSubject
{
    /// <summary>
    /// A message of the chat page, whose chat compressing continues in a new chat or in place, see ChatCompressionTarget.
    /// </summary>
    CHAT,

    /// <summary>
    /// A message of the embedded chat of an assistant, whose earlier messages compressing replaces with the summary.
    /// </summary>
    EMBEDDED_CHAT,

    /// <summary>
    /// A request of an assistant, together with its files, which nothing compresses.
    /// </summary>
    ASSISTANT_REQUEST,
}