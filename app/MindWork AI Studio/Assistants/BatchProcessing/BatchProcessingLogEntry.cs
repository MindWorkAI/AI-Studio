namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// One row of the log of a previous batch run.
/// </summary>
public sealed record BatchProcessingLogEntry(string RelativePath, string Time, string Model, string Status, string Details)
{
    public bool WasSuccessful => string.Equals(this.Status, nameof(BatchProcessingFileStatus.DONE), StringComparison.OrdinalIgnoreCase);
}