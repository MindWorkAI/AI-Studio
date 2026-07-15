namespace AIStudio.Tools.Media;

/// <summary>Terminal batch outcome retained until its owner is displayed.</summary>
public sealed record MediaImportOutcome
{
    public required MediaImportOwner Owner { get; init; }

    public required MediaImportStatus Status { get; init; }

    public IReadOnlyList<MediaImportFailure> Failures { get; init; } = [];

    public IReadOnlyList<MediaImportWarning> Warnings { get; init; } = [];
}