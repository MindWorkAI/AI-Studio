using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines one bounded declarative interaction control.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("42306121")]
public sealed class VisualBriefingControlSpec
{
    /// <summary>Gets or sets the globally unique control identifier.</summary>
    [JsonRequired]
    public string ControlId { get; set; } = string.Empty;

    /// <summary>Gets or sets the owning component identifier.</summary>
    [JsonRequired]
    public string ComponentId { get; set; } = string.Empty;

    /// <summary>Gets or sets the control kind.</summary>
    [JsonRequired]
    public VisualBriefingControlKind Kind { get; set; }

    /// <summary>Gets or sets the deterministic initial value.</summary>
    [JsonRequired]
    public JsonElement InitialValue { get; set; }

    /// <summary>Gets or sets the selectable options.</summary>
    [JsonRequired]
    public List<VisualBriefingControlOption> Options { get; set; } = [];
}