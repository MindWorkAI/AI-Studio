namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes the outcome of committing one immutable visual briefing revision.
/// </summary>
/// <param name="Success">Whether the revision was committed.</param>
/// <param name="Version">The committed version metadata.</param>
/// <param name="Issue">The user-safe commit issue.</param>
public sealed record VisualBriefingRevisionResult(bool Success, VisualBriefingVersion? Version, string Issue)
{
    /// <summary>
    /// Creates a failed revision result.
    /// </summary>
    /// <param name="issue">The user-safe commit issue.</param>
    /// <returns>The failed revision result.</returns>
    public static VisualBriefingRevisionResult Failure(string issue) => new(false, null, issue);
}