using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines the bounded semantic input for one compiled chart.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("68b2ff45")]
public sealed class VisualBriefingChartSpec
{
    /// <summary>Gets or sets the owning component identifier.</summary>
    [JsonRequired]
    public string ComponentId { get; set; } = string.Empty;

    /// <summary>Gets or sets the chart presentation kind.</summary>
    [JsonRequired]
    public VisualBriefingChartKind Kind { get; set; }

    /// <summary>Gets or sets the ordered category labels.</summary>
    [JsonRequired]
    public List<string> Categories { get; set; } = [];

    /// <summary>Gets or sets the chart's numeric series.</summary>
    [JsonRequired]
    public List<VisualBriefingChartSeries> Series { get; set; } = [];
}