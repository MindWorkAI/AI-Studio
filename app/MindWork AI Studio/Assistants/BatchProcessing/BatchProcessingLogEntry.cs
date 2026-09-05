namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// One row of the log of a previous batch run.
/// </summary>
/// <remarks>
/// The tools column arrived later than the rest. A log written before it existed
/// leaves it empty, which is also what a run without any tool call looks like.
/// </remarks>
public sealed record BatchProcessingLogEntry(string RelativePath, string Time, string Model, string Status, string Details, string UsedTools = "")
{
    public bool WasSuccessful => string.Equals(this.Status, nameof(BatchProcessingFileStatus.DONE), StringComparison.OrdinalIgnoreCase);
}