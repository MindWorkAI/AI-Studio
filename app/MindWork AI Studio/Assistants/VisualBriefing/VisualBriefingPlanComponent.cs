using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Plans one semantic component and its evidence and content dependencies.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("bdafbeaf")]
public sealed class VisualBriefingPlanComponent
{
    /// <summary>Gets or sets the globally unique component identifier.</summary>
    [JsonRequired]
    public string ComponentId { get; set; } = string.Empty;

    /// <summary>Gets or sets the component kind.</summary>
    [JsonRequired]
    public VisualBriefingComponentKind Kind { get; set; }

    /// <summary>Gets or sets the referenced evidence identifiers.</summary>
    [JsonRequired]
    public List<string> EvidenceIds { get; set; } = [];

    /// <summary>Gets or sets the component's planned semantic slots.</summary>
    [JsonRequired]
    public List<VisualBriefingPlanSlot> Slots { get; set; } = [];

    /// <summary>Gets or sets the optional embedded asset identifier.</summary>
    [JsonRequired]
    public string? AssetId { get; set; }

    /// <summary>Gets or sets the orientation used only by timeline components.</summary>
    [JsonRequired]
    public VisualBriefingTimelineOrientation? TimelineOrientation { get; set; }
}