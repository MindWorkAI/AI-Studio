namespace AIStudio.Settings.DataModel;

/// <summary>
/// How an assistant which works on documents as well as on texts starts.
/// </summary>
public enum AssistantStartMode
{
    /// <summary>
    /// The assistant asks every time whether the user wants to work on a document or on a text.
    /// </summary>
    /// <remarks>
    /// This must stay the first member. Enums are persisted under their name, and an unknown name
    /// falls back to the default value of the enum, which is the member with the value zero.
    /// </remarks>
    ASK_EVERY_TIME,

    /// <summary>
    /// The assistant always starts with a document.
    /// </summary>
    DOCUMENT,

    /// <summary>
    /// The assistant always starts with a text.
    /// </summary>
    TEXT,
}