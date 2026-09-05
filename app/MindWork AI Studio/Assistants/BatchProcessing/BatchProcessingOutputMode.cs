namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// How the results of a batch run are written to disk.
/// </summary>
public enum BatchProcessingOutputMode
{
    /// <summary>
    /// One result file per processed document, written in the chosen file format.
    /// </summary>
    /// <remarks>
    /// This must stay the first member. Enums are persisted under their name, and an unknown name
    /// falls back to the default value of the enum, which is the member with the value zero. That
    /// is what lets settings written before this member was renamed still land here.
    /// </remarks>
    INDIVIDUAL_FILES,

    /// <summary>
    /// A CSV results table, where each AI answer becomes one row. The content of
    /// the result column is defined by the instructions of the batch run.
    /// </summary>
    TABLE_ONLY,
}