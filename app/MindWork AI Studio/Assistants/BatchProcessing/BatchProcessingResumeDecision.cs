namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// What should happen when a previous batch run was found in the output folder.
/// </summary>
public enum BatchProcessingResumeDecision
{
    /// <summary>
    /// Process only the documents which are missing in the log or which failed
    /// during the previous run.
    /// </summary>
    CONTINUE,

    /// <summary>
    /// Process all documents again and replace the previous log.
    /// </summary>
    RESTART,
}