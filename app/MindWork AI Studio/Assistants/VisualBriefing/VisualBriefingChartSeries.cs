using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines one named numeric series in a chart specification.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("57679f28")]
public sealed class VisualBriefingChartSeries
{
    /// <summary>Gets or sets the series name.</summary>
    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the ordered numeric values.</summary>
    [JsonRequired]
    public List<decimal> Values { get; set; } = [];
}