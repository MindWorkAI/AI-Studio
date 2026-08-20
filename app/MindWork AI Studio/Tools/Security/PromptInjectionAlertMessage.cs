namespace AIStudio.Tools.Security;

/// <summary>
/// Asks the UI to tell the user what was filtered out of the content they just used.
/// </summary>
/// <remarks>
/// Carries every result of one user action rather than a single one. Attaching twenty
/// documents at once must produce one dialog listing all of them, not twenty dialogs.
/// </remarks>
/// <param name="Results">What was filtered, per piece of content.</param>
public sealed record PromptInjectionAlertMessage(IReadOnlyList<PromptInjectionScanResult> Results)
{
    /// <summary>
    /// Gets the total number of filtered passages across all content.
    /// </summary>
    public int TotalRedactedCount => this.Results.Sum(result => result.RedactedCount);
}