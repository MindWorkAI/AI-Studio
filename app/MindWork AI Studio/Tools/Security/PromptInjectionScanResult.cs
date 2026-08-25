namespace AIStudio.Tools.Security;

/// <summary>
/// What the runtime filtered out of one piece of external content.
/// </summary>
/// <param name="Source">Where the content came from, so the user can tell which file or page it was.</param>
/// <param name="Findings">The passages that were removed. Capped by the runtime.</param>
/// <param name="RedactedCount">How many passages were removed in total, which may exceed the number of findings.</param>
public sealed record PromptInjectionScanResult(PromptInjectionSource Source, IReadOnlyList<PromptInjectionFinding> Findings, int RedactedCount)
{
    /// <summary>
    /// Gets a value indicating whether anything was filtered out of this content.
    /// </summary>
    /// <remarks>
    /// The content itself stays usable either way: passages are removed, the content around
    /// them is not rejected.
    /// </remarks>
    public bool WasFiltered => this.RedactedCount > 0;
}