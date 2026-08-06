namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingVersions</c> for the visual briefing feature.
/// </summary>
public static class VisualBriefingVersions
{
    /// <summary>Gets the standalone artifact contract version.</summary>
    public const int ARTIFACT = 1;
    
    /// <summary>Gets the project manifest contract version.</summary>
    public const int MANIFEST = 1;
    
    /// <summary>Gets the canonical data schema version.</summary>
    public const int SCHEMA = 2;
    
    /// <summary>
    /// Gets the deterministic HTML, CSS, chart, and interaction compiler version. Increment this
    /// whenever compiler behavior changes so interrupted recompiles cannot resume across versions.
    /// </summary>
    public const int COMPILER = 4;
    
    /// <summary>
    /// Gets the embedded AI Studio runtime bundle version. Increment this for changes to the
    /// runtime script or bundled Apache ECharts distribution.
    /// </summary>
    public const int RUNTIME = 1;
    
    /// <summary>Gets the formula-tree contract version.</summary>
    public const int FORMULA = 1;
    
    /// <summary>Gets the persistent build-record contract version.</summary>
    public const int BUILD = 1;
    
    /// <summary>Gets the immutable intermediate-artifact contract version.</summary>
    public const int INTERMEDIATE_ARTIFACT = 2;
    
    /// <summary>Gets the evidence-agent response contract version.</summary>
    public const int EVIDENCE_CONTRACT = 2;
    
    /// <summary>Gets the plan-agent response contract version.</summary>
    public const int PLAN_CONTRACT = 2;
    
    /// <summary>Gets the content-agent response contract version.</summary>
    public const int CONTENT_CONTRACT = 2;
    
    /// <summary>Gets the design-agent response contract version.</summary>
    public const int DESIGN_CONTRACT = 2;
}