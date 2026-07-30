using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stores an immutable validated evidence-stage artifact.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingEvidenceArtifact
{
    /// <summary>Gets or sets the intermediate artifact schema version.</summary>
    public int ArtifactVersion { get; set; } = VisualBriefingVersions.INTERMEDIATE_ARTIFACT;
    
    /// <summary>Gets or sets the evidence prompt contract version.</summary>
    public int ContractVersion { get; set; } = VisualBriefingVersions.EVIDENCE_CONTRACT;
    
    /// <summary>Gets or sets the immutable artifact identifier.</summary>
    public Guid ArtifactId { get; init; }
    
    /// <summary>Gets or sets the artifact creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
    
    /// <summary>Gets or sets the hash of the artifact payload.</summary>
    public string PayloadHash { get; init; } = string.Empty;
    
    /// <summary>Gets or sets the extracted factual statements.</summary>
    public List<VisualBriefingEvidenceFact> Facts { get; init; } = [];
    
    /// <summary>Gets or sets the extracted numeric metrics.</summary>
    public List<VisualBriefingEvidenceMetric> Metrics { get; init; } = [];
    
    /// <summary>Gets or sets the extracted tables.</summary>
    public List<VisualBriefingEvidenceTable> Tables { get; init; } = [];
    
    /// <summary>Gets or sets source coverage.</summary>
    public List<VisualBriefingSourceCoverage> SourceCoverage { get; init; } = [];
    
    /// <summary>Gets or sets the visual asset plan.</summary>
    public List<VisualBriefingAssetPlanItem> AssetPlan { get; init; } = [];
    
    /// <summary>Gets or sets the contributing model name.</summary>
    public string Model { get; init; } = string.Empty;
}