namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Computes the payload hashes that decide whether a stored intermediate artifact is still usable.
/// </summary>
/// <remarks>
/// Each formula lives here exactly once. The stage that writes an artifact and the store that reads it
/// back have to agree on the sections down to their order, and they used to spell the formula out on
/// both sides with a comment asking the next developer to keep them aligned. A single misplaced section
/// makes the store discard every stored artifact of that kind, and it reports that as a missing
/// artifact rather than as an error, so the mistake surfaces as a briefing that silently refuses to be
/// reused. Sections are canonical JSON, which additionally makes the hashes independent of the order in
/// which the artifact properties are declared.
/// </remarks>
internal static class VisualBriefingPayloadHash
{
    /// <summary>
    /// Computes the payload hash of an evidence artifact.
    /// </summary>
    /// <param name="facts">The extracted facts.</param>
    /// <param name="metrics">The extracted metrics.</param>
    /// <param name="tables">The extracted tables.</param>
    /// <param name="sourceCoverage">The per-source coverage.</param>
    /// <param name="assetPlan">The planned visual assets.</param>
    /// <returns>The payload hash.</returns>
    internal static string ForEvidence(
        List<VisualBriefingEvidenceFact> facts,
        List<VisualBriefingEvidenceMetric> metrics,
        List<VisualBriefingEvidenceTable> tables,
        List<VisualBriefingSourceCoverage> sourceCoverage,
        List<VisualBriefingAssetPlanItem> assetPlan) =>
        VisualBriefingHashing.ComputeSections(
            VisualBriefingHashing.CanonicalJson(facts),
            VisualBriefingHashing.CanonicalJson(metrics),
            VisualBriefingHashing.CanonicalJson(tables),
            VisualBriefingHashing.CanonicalJson(sourceCoverage),
            VisualBriefingHashing.CanonicalJson(assetPlan));

    /// <summary>
    /// Computes the payload hash of a plan artifact.
    /// </summary>
    /// <param name="sections">The planned sections.</param>
    /// <param name="structuralSignature">The structural signature of the plan.</param>
    /// <returns>The payload hash.</returns>
    internal static string ForPlan(
        List<VisualBriefingPlanSection> sections,
        string structuralSignature) => VisualBriefingHashing.ComputeSections(VisualBriefingHashing.CanonicalJson(sections), structuralSignature);

    /// <summary>
    /// Computes the payload hash of a content artifact.
    /// </summary>
    /// <param name="slots">The filled content slots.</param>
    /// <param name="charts">The chart specifications.</param>
    /// <param name="controls">The interactive control specifications.</param>
    /// <param name="formulas">The formula specifications.</param>
    /// <param name="accessibilityTexts">The accessibility texts per component.</param>
    /// <param name="sourceReferences">The source references per component.</param>
    /// <param name="resetLabel">The localized reset label.</param>
    /// <param name="sourceCoverage">The per-source coverage.</param>
    /// <param name="assetPlan">The planned visual assets.</param>
    /// <param name="structuralSignature">The structural signature of the business data.</param>
    /// <returns>The payload hash.</returns>
    internal static string ForContent(
        List<VisualBriefingSlotValue> slots,
        List<VisualBriefingChartSpec> charts,
        List<VisualBriefingControlSpec> controls,
        List<VisualBriefingFormulaSpec> formulas,
        Dictionary<string, string> accessibilityTexts,
        Dictionary<string, List<string>> sourceReferences,
        string resetLabel,
        List<VisualBriefingSourceCoverage> sourceCoverage,
        List<VisualBriefingAssetPlanItem> assetPlan,
        string structuralSignature) =>
        VisualBriefingHashing.ComputeSections(
            VisualBriefingHashing.CanonicalJson(slots),
            VisualBriefingHashing.CanonicalJson(charts),
            VisualBriefingHashing.CanonicalJson(controls),
            VisualBriefingHashing.CanonicalJson(formulas),
            VisualBriefingHashing.CanonicalJson(accessibilityTexts),
            VisualBriefingHashing.CanonicalJson(sourceReferences),
            resetLabel,
            VisualBriefingHashing.CanonicalJson(sourceCoverage),
            VisualBriefingHashing.CanonicalJson(assetPlan),
            structuralSignature);

    /// <summary>
    /// Computes the payload hash of a presentation artifact.
    /// </summary>
    /// <param name="layout">The compiled layout tree.</param>
    /// <param name="profile">The design profile.</param>
    /// <param name="templateHash">The hash of the compiled template.</param>
    /// <param name="cssHash">The hash of the compiled CSS.</param>
    /// <returns>The payload hash.</returns>
    internal static string ForPresentation(
        VisualBriefingLayoutNode layout,
        VisualBriefingDesignProfile profile,
        string templateHash,
        string cssHash) =>
        VisualBriefingHashing.ComputeSections(
            VisualBriefingHashing.CanonicalJson(layout),
            profile.ToString(), templateHash, cssHash);
}