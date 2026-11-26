namespace AIStudio.Assistants;

/// <summary>
/// What an assistant which works on documents as well as on texts works on right now.
/// </summary>
public enum AssistantContentMode
{
    /// <summary>
    /// The user has not chosen yet, so the assistant shows nothing but the choice.
    /// </summary>
    UNDECIDED,

    /// <summary>
    /// The assistant works on a document and writes its result into a new file.
    /// </summary>
    DOCUMENT,

    /// <summary>
    /// The assistant works on a text and shows its result right away.
    /// </summary>
    TEXT,
}