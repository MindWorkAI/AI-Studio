namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Provides safe list metadata even when the persisted manifest cannot be deserialized.
/// </summary>
internal sealed record VisualBriefingProjectEntry(Guid BriefingId, string Name, DateTimeOffset ModifiedAtUtc, VisualBriefingProjectLoadStatus Status, VisualBriefingManifest? Manifest)
{
    /// <summary>Gets whether the project can be opened normally.</summary>
    public bool IsAvailable => this.Status is VisualBriefingProjectLoadStatus.AVAILABLE && this.Manifest is not null;

    /// <summary>Creates an available project entry from a validated manifest.</summary>
    public static VisualBriefingProjectEntry FromManifest(VisualBriefingManifest manifest) => new(manifest.BriefingId, manifest.Name, manifest.ModifiedAtUtc, VisualBriefingProjectLoadStatus.AVAILABLE, manifest);
}