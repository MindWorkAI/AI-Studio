namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// The outcome of importing one single chat.
/// </summary>
public enum ChatArchiveImportOutcome
{
    /// <summary>
    /// The chat was written to the chat storage.
    /// </summary>
    IMPORTED,

    /// <summary>
    /// The chat already existed and was left alone.
    /// </summary>
    SKIPPED,

    /// <summary>
    /// The chat could not be imported.
    /// </summary>
    FAILED,
}