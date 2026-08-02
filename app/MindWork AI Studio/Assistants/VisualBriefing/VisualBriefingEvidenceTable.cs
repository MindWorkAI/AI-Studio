using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes one sourced table extracted during evidence analysis.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("ad23c5b0")]
public sealed class VisualBriefingEvidenceTable
{
    /// <summary>Gets or sets the stable evidence identifier.</summary>
    [JsonRequired]
    public string EvidenceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the table title.</summary>
    [JsonRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the ordered column names.</summary>
    [JsonRequired]
    public List<string> Columns { get; set; } = [];

    /// <summary>Gets or sets the ordered table rows.</summary>
    [JsonRequired]
    public List<List<JsonElement>> Rows { get; set; } = [];

    /// <summary>Gets or sets the source handles supporting the table.</summary>
    [JsonRequired]
    public List<string> SourceIds { get; set; } = [];
}