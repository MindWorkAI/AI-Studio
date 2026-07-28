namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingImportResult</c> for the visual briefing feature.
/// </summary>
public sealed record VisualBriefingImportResult(
    bool Success,
    Guid BriefingId,
    Guid RevisionId,
    bool RequiresCopyConfirmation,
    bool WasDeduplicated,
    string Issue);