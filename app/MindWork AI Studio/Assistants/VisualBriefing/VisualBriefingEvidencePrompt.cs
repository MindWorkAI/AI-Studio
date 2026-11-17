namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// The evidence which the plan and content stages show their models.
/// </summary>
/// <param name="Facts">The facts of the evidence.</param>
/// <param name="Metrics">The metrics of the evidence.</param>
/// <param name="Tables">The tables of the evidence.</param>
/// <param name="AssetPlan">The planned pictures of the briefing.</param>
internal sealed record VisualBriefingEvidencePrompt(
    List<VisualBriefingEvidenceFact> Facts,
    List<VisualBriefingEvidenceMetric> Metrics,
    List<VisualBriefingEvidenceTable> Tables,
    List<VisualBriefingAssetPlanItem> AssetPlan)
{
    /// <summary>
    /// Gathers what a model gets to see of an evidence artifact.
    /// </summary>
    /// <param name="evidence">The evidence artifact.</param>
    /// <returns>The evidence for the prompt.</returns>
    internal static VisualBriefingEvidencePrompt Of(VisualBriefingEvidenceArtifact evidence) => new(evidence.Facts, evidence.Metrics, evidence.Tables, evidence.AssetPlan);
}