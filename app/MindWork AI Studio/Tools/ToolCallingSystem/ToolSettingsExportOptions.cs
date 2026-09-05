namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// The administrator's choices for one export. The dialog initially selects every available area.
/// </summary>
public sealed record ToolSettingsExportOptions
{
    public IReadOnlySet<string> SelectedAreaIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public ToolSettingsExportMode Mode { get; init; } = ToolSettingsExportMode.LOCKED;

    public bool IncludeSecrets { get; init; }

    public bool IncludeMinimumProviderConfidence { get; init; } = true;
}