using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Records how one source contributed to canonical content.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("b1535c0e")]
public sealed class VisualBriefingSourceCoverage
{
    /// <summary>
    /// Gets or sets the source handle, see <c>VisualBriefingSourceHandles</c>.
    /// </summary>
    [JsonRequired]
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the coverage classification.
    /// </summary>
    [JsonRequired]
    public VisualBriefingSourceCoverageKind Coverage { get; set; }

    /// <summary>
    /// Gets or sets a short, non-sensitive explanation.
    /// </summary>
    [JsonRequired]
    public string Reason { get; set; } = string.Empty;
}