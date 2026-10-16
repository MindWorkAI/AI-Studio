using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stores an immutable validated plan-stage artifact.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingPlanArtifact
{
    /// <summary>Gets or sets the intermediate artifact schema version.</summary>
    public int ArtifactVersion { get; set; } = VisualBriefingVersions.INTERMEDIATE_ARTIFACT;
    
    /// <summary>Gets or sets the plan prompt contract version.</summary>
    public int ContractVersion { get; set; } = VisualBriefingVersions.PLAN_CONTRACT;
    
    /// <summary>Gets or sets the immutable artifact identifier.</summary>
    public Guid ArtifactId { get; init; }
    
    /// <summary>Gets or sets the artifact creation time.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
    
    /// <summary>Gets or sets the hash of the artifact payload.</summary>
    public string PayloadHash { get; init; } = string.Empty;
    
    /// <summary>Gets or sets the ordered planned sections.</summary>
    public List<VisualBriefingPlanSection> Sections { get; init; } = [];
    
    /// <summary>Gets or sets the canonical structural signature.</summary>
    public string StructuralSignature { get; init; } = string.Empty;
    
    /// <summary>Gets or sets the contributing model name.</summary>
    public string Model { get; init; } = string.Empty;
}