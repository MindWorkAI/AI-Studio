namespace AIStudio.Settings.DataModel;

/// <summary>
/// What happens to the inputs of an assistant when the user leaves it, e.g., for the chat or for another assistant.
/// </summary>
public enum AssistantInputsWhenLeaving
{
    /// <summary>
    /// The assistant keeps the inputs, the options, and its last result in memory until the user resets it, sends
    /// a new text to it, or quits AI Studio.
    /// </summary>
    KEEP_INPUTS,

    /// <summary>
    /// The assistant starts anew when the user opens it again. A run which goes on keeps its state all the same.
    /// </summary>
    DISCARD_INPUTS,
}