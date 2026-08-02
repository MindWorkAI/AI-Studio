namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes the outcome of importing a standalone visual briefing artifact.
/// </summary>
/// <param name="Success">Whether the import completed successfully.</param>
/// <param name="BriefingId">The local briefing identifier.</param>
/// <param name="RevisionId">The imported immutable revision identifier.</param>
/// <param name="RequiresCopyConfirmation">Whether the user must confirm importing under a new briefing identifier.</param>
/// <param name="WasDeduplicated">Whether an identical local revision already existed.</param>
/// <param name="Issue">The user-safe import issue.</param>
public sealed record VisualBriefingImportResult(
    bool Success,
    Guid BriefingId,
    Guid RevisionId,
    bool RequiresCopyConfirmation,
    bool WasDeduplicated,
    string Issue);