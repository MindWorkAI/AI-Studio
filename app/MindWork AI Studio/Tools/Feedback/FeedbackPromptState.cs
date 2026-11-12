namespace AIStudio.Tools.Feedback;

/// <summary>
/// Where the weekly feedback prompt stands.
/// </summary>
public enum FeedbackPromptState
{
    /// <summary>
    /// Nothing to show: the feedback is switched off, not due, or shown in another chat.
    /// </summary>
    NONE,

    /// <summary>
    /// The weekly feedback is due: the prompt asks the user.
    /// </summary>
    ASK,

    /// <summary>
    /// The feedback was sent: the prompt thanks the user until the next answer.
    /// </summary>
    THANKED,
}