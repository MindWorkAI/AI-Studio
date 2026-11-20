namespace AIStudio.Settings.DataModel;

/// <summary>
/// What happens to the assistants of a category the user collapsed or hid on the assistants page.
/// </summary>
public enum AssistantOfferBehavior
{
    /// <summary>
    /// AI Studio keeps offering the assistants outside of the assistants page, e.g., in the "Send to ..." menu.
    /// </summary>
    KEEP_OFFERING,

    /// <summary>
    /// AI Studio treats the assistants as if they did not exist, e.g., the "Send to ..." menu leaves them out.
    /// </summary>
    TREAT_AS_ABSENT,
}