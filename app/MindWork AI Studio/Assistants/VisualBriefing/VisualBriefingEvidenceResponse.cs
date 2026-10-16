using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines the strict structured response returned by the evidence agent.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingEvidenceResponse
{
    /// <summary>Gets or sets the evidence contract version.</summary>
    [JsonRequired]
    public int ContractVersion { get; set; }

    /// <summary>Gets or sets the extracted factual statements.</summary>
    [JsonRequired]
    public List<VisualBriefingEvidenceFact> Facts { get; set; } = [];

    /// <summary>Gets or sets the extracted numeric metrics.</summary>
    [JsonRequired]
    public List<VisualBriefingEvidenceMetric> Metrics { get; set; } = [];

    /// <summary>Gets or sets the extracted tables.</summary>
    [JsonRequired]
    public List<VisualBriefingEvidenceTable> Tables { get; set; } = [];

    /// <summary>Gets or sets the exactly-once source coverage declarations.</summary>
    [JsonRequired]
    public List<VisualBriefingSourceCoverage> SourceCoverage { get; set; } = [];

    /// <summary>Gets or sets the planned use of supplied visual assets.</summary>
    [JsonRequired]
    public List<VisualBriefingAssetPlanItem> AssetPlan { get; set; } = [];
}