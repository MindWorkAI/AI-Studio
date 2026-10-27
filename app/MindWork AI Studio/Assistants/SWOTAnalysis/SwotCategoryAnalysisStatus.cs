namespace AIStudio.Assistants.SWOTAnalysis;

public enum SwotCategoryAnalysisStatus
{
    RUNNING,

    /// <summary>
    /// The first answer did not have the expected form, and the model is asked once more.
    /// </summary>
    REPAIRING,

    COMPLETED,
    FAILED,
    CANCELED,
}