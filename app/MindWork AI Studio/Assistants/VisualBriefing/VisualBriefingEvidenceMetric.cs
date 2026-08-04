using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes one sourced numeric metric extracted during evidence analysis.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("08d12050")]
public sealed class VisualBriefingEvidenceMetric
{
    /// <summary>Gets or sets the stable evidence identifier.</summary>
    [JsonRequired]
    public string EvidenceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the metric label.</summary>
    [JsonRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the numeric value.</summary>
    [JsonRequired]
    public decimal Value { get; set; }

    /// <summary>Gets or sets the value unit.</summary>
    [JsonRequired]
    public string Unit { get; set; } = string.Empty;

    /// <summary>Gets or sets the source handles supporting the metric.</summary>
    [JsonRequired]
    public List<string> SourceIds { get; set; } = [];
}