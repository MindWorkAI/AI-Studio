namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingVersion</c> for the visual briefing feature.
/// </summary>
public sealed class VisualBriefingVersion
{
    /// <summary>Gets or sets the canonical data schema used by this revision.</summary>
    public int SchemaVersion { get; set; } = VisualBriefingVersions.SCHEMA;

    /// <summary>Gets or sets the semantic intermediate-artifact format.</summary>
    public int IntermediateArtifactVersion { get; set; } = VisualBriefingVersions.INTERMEDIATE_ARTIFACT;

    /// <summary>Gets or sets the evidence contract used by this revision.</summary>
    public int EvidenceContractVersion { get; set; } = VisualBriefingVersions.EVIDENCE_CONTRACT;

    /// <summary>Gets or sets the plan contract used by this revision.</summary>
    public int PlanContractVersion { get; set; } = VisualBriefingVersions.PLAN_CONTRACT;

    /// <summary>Gets or sets the content contract used by this revision.</summary>
    public int ContentContractVersion { get; set; } = VisualBriefingVersions.CONTENT_CONTRACT;

    /// <summary>Gets or sets the design contract used by this revision.</summary>
    public int DesignContractVersion { get; set; } = VisualBriefingVersions.DESIGN_CONTRACT;

    /// <summary>
    /// Defines <c>VersionNumber</c> for the visual briefing feature.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Defines <c>RevisionId</c> for the visual briefing feature.
    /// </summary>
    public Guid RevisionId { get; set; }

    /// <summary>
    /// Defines <c>ParentRevisionId</c> for the visual briefing feature.
    /// </summary>
    public Guid? ParentRevisionId { get; set; }

    /// <summary>
    /// Defines <c>CreatedAtUtc</c> for the visual briefing feature.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Defines <c>EditMode</c> for the visual briefing feature.
    /// </summary>
    public VisualBriefingEditMode EditMode { get; set; }

    /// <summary>
    /// Defines <c>Instruction</c> for the visual briefing feature.
    /// </summary>
    public string Instruction { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash of the complete standalone HTML document.
    /// </summary>
    public string DocumentHash { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>Origin</c> for the visual briefing feature.
    /// </summary>
    public string Origin { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>FileName</c> for the visual briefing feature.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>DataHash</c> for the visual briefing feature.
    /// </summary>
    public string DataHash { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>AssetHash</c> for the visual briefing feature.
    /// </summary>
    public string AssetHash { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>TemplateHash</c> for the visual briefing feature.
    /// </summary>
    public string TemplateHash { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>CssHash</c> for the visual briefing feature.
    /// </summary>
    public string CssHash { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>RuntimeHash</c> for the visual briefing feature.
    /// </summary>
    public string RuntimeHash { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>ContentArtifactId</c> for the visual briefing feature.
    /// </summary>
    public Guid? ContentArtifactId { get; set; }

    /// <summary>
    /// Defines <c>EvidenceArtifactId</c> for the visual briefing feature.
    /// </summary>
    public Guid? EvidenceArtifactId { get; set; }

    /// <summary>
    /// Defines <c>PlanArtifactId</c> for the visual briefing feature.
    /// </summary>
    public Guid? PlanArtifactId { get; set; }

    /// <summary>
    /// Defines <c>PresentationArtifactId</c> for the visual briefing feature.
    /// </summary>
    public Guid? PresentationArtifactId { get; set; }

    /// <summary>
    /// Defines <c>BuildId</c> for the visual briefing feature.
    /// </summary>
    public Guid? BuildId { get; set; }

    /// <summary>
    /// Defines <c>OperationId</c> for the visual briefing feature.
    /// </summary>
    public Guid? OperationId { get; set; }

    /// <summary>
    /// Defines <c>ModelContributions</c> for the visual briefing feature.
    /// </summary>
    public List<VisualBriefingModelContribution> ModelContributions { get; set; } = [];
}