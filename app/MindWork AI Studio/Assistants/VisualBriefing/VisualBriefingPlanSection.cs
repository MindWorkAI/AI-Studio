using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Plans one narrative section and its ordered components.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("91d1394d")]
public sealed class VisualBriefingPlanSection
{
    /// <summary>Gets or sets the globally unique section identifier.</summary>
    [JsonRequired]
    public string SectionId { get; set; } = string.Empty;

    /// <summary>Gets or sets the narrative purpose of the section.</summary>
    [JsonRequired]
    public VisualBriefingSectionRole Role { get; set; }

    /// <summary>Gets or sets the slot containing the section title.</summary>
    [JsonRequired]
    public string TitleSlotId { get; set; } = string.Empty;

    /// <summary>Gets or sets the slot containing the section summary.</summary>
    [JsonRequired]
    public string SummarySlotId { get; set; } = string.Empty;

    /// <summary>Gets or sets the ordered planned components.</summary>
    [JsonRequired]
    public List<VisualBriefingPlanComponent> Components { get; set; } = [];
}