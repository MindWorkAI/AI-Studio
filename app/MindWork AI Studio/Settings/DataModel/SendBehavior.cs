namespace AIStudio.Settings.DataModel;

/// <summary>
/// Possible behaviors for sending the input to the AI.
/// </summary>
public enum SendBehavior
{
    /// <summary>
    /// There is no shortcut to send the input to the AI. The user must click
    /// the send button. The enter key adds a new line.
    /// </summary>
    NO_KEY_IS_SENDING,
    
    /// <summary>
    /// The user can send the input to the AI by pressing any modifier key
    /// together with the enter key. Alternatively, the user can click the sent
    /// button. The enter key alone adds a new line.
    /// </summary>
    MODIFER_ENTER_IS_SENDING,
    
    /// <summary>
    /// The user can send the input to the AI by pressing the enter key. In order
    /// to add a new line, the user must press the shift key together with the
    /// enter key. Alternatively, the user can click the send button to send the
    /// input to the AI.
    /// </summary>
    ENTER_IS_SENDING,
}