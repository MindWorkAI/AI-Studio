using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Assigns a validated JSON value to one planned semantic slot.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("6cfa3f02")]
public sealed class VisualBriefingSlotValue
{
    /// <summary>Gets or sets the planned slot identifier.</summary>
    [JsonRequired]
    public string SlotId { get; init; } = string.Empty;

    /// <summary>Gets or sets the validated slot value.</summary>
    [JsonRequired]
    public JsonElement Value { get; init; }
}