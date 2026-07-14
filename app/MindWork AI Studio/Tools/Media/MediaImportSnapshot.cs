namespace AIStudio.Tools.Media;

/// <summary>Copied owner-specific state suitable for rendering after navigation.</summary>
public sealed record MediaImportSnapshot
{
    public required MediaImportOwner Owner { get; init; }

    public required MediaImportTarget Target { get; init; }

    public required MediaTranscriptionPhase Phase { get; init; }

    public required MediaImportStatus Status { get; init; }

    public string CurrentFileName { get; init; } = string.Empty;

    public double? Progress { get; init; }

    public bool IsBusy => this.Status is MediaImportStatus.QUEUED or MediaImportStatus.RUNNING or MediaImportStatus.CANCELING;
}