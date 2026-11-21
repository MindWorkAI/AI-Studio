namespace AIStudio.Tools.RAG;

/// <summary>
/// The passages which the check of which passages fit the user prompt left, and why it left out the others.
/// </summary>
/// <param name="Contexts">The passages left, in the order of the results of the check.</param>
/// <param name="NumJudged">How many passages the check judged, i.e., got a usable answer for.</param>
/// <param name="NumUnchecked">How many passages stayed without a judgment, because their check failed.</param>
/// <param name="NumRejected">How many passages were left out, because the check judged them not to fit.</param>
/// <param name="NumBelowThreshold">How many passages judged to fit were left out, because their confidence fell below the threshold.</param>
/// <param name="Threshold">The confidence a passage judged to fit needed to stay.</param>
public sealed record ValidatedPassages(IReadOnlyList<PreparedRetrievalContext> Contexts, int NumJudged, int NumUnchecked, int NumRejected, int NumBelowThreshold, float Threshold)
{
    /// <summary>
    /// Whether the check failed for every passage, so that it judged none of them.
    /// </summary>
    public bool IsUnchecked => this.NumUnchecked > 0 && this.NumJudged is 0;
}