using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Plans one semantic content slot owned by a component.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("04cc2e77")]
public sealed class VisualBriefingPlanSlot
{
    /// <summary>Gets or sets the globally unique slot identifier.</summary>
    [JsonRequired]
    public string SlotId { get; set; } = string.Empty;

    /// <summary>Gets or sets the semantic purpose of the slot.</summary>
    [JsonRequired]
    public VisualBriefingSlotRole Role { get; set; }
}