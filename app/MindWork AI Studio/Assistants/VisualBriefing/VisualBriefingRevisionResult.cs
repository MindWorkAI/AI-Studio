namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingRevisionResult</c> for the visual briefing feature.
/// </summary>
public sealed record VisualBriefingRevisionResult(bool Success, VisualBriefingVersion? Version, string Issue)
{
    /// <summary>
    /// Defines <c>Failure</c> for the visual briefing feature.
    /// </summary>
    public static VisualBriefingRevisionResult Failure(string issue) => new(false, null, issue);
}