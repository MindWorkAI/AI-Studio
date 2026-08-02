using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes one sourced factual statement extracted during evidence analysis.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("7857e7da")]
public sealed class VisualBriefingEvidenceFact
{
    /// <summary>Gets or sets the stable evidence identifier.</summary>
    [JsonRequired]
    public string EvidenceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the factual statement.</summary>
    [JsonRequired]
    public string Statement { get; set; } = string.Empty;

    /// <summary>Gets or sets the source handles supporting the statement.</summary>
    [JsonRequired]
    public List<string> SourceIds { get; set; } = [];
}