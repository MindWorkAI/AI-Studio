namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// How the results of a batch run are written to disk.
/// </summary>
public enum BatchProcessingOutputMode
{
    /// <summary>
    /// One Markdown result file per processed document.
    /// </summary>
    MARKDOWN_FILES,

    /// <summary>
    /// A CSV results table, where each AI answer becomes one row. The content of
    /// the result column is defined by the instructions of the batch run.
    /// </summary>
    TABLE_ONLY,
}