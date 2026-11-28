namespace AIStudio.Tools.Web;

/// <summary>
/// What went wrong while loading web content, so that the reader can tell the user, see WebContentImportResult.
/// </summary>
public enum WebContentImportIssue
{
    /// <summary>
    /// The content arrived as asked for.
    /// </summary>
    NONE,

    /// <summary>
    /// The content arrived, but uncleaned, since no model is available for the content cleaner.
    /// </summary>
    NOT_CLEANED_NO_MODEL,

    /// <summary>
    /// The content arrived, but uncleaned, since the model of the content cleaner gave no answer.
    /// </summary>
    NOT_CLEANED_NO_ANSWER,

    /// <summary>
    /// The content could not be loaded, see WebContentImportResult.ErrorMessage.
    /// </summary>
    LOAD_FAILED,
}