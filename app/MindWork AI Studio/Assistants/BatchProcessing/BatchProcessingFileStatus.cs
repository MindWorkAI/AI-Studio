namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// The processing state of one file within a batch run.
/// </summary>
public enum BatchProcessingFileStatus
{
    QUEUED,
    PROCESSING,
    DONE,
    FAILED,
    CANCELED,
}