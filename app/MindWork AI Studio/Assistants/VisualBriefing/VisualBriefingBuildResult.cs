namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Contains the terminal result of one visual briefing build.
/// </summary>
/// <param name="Success">Whether a revision was committed.</param>
/// <param name="Version">The committed immutable version.</param>
/// <param name="Issue">The user-safe issue.</param>
/// <param name="FailureCode">The stable failure code.</param>
/// <param name="Diagnostics">Safe technical diagnostics.</param>
/// <param name="CanContinueAsRebuild">Whether incompatible valid content can continue without another content call.</param>
internal sealed record VisualBriefingBuildResult(
    bool Success,
    VisualBriefingVersion? Version,
    string Issue,
    VisualBriefingFailureCode FailureCode,
    VisualBriefingOperationDiagnostics Diagnostics,
    bool CanContinueAsRebuild);
