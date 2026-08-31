using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Connects one deterministic formula tree to a component result slot.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("b644b191")]
public sealed class VisualBriefingFormulaSpec
{
    /// <summary>Gets or sets the owning component identifier.</summary>
    [JsonRequired]
    public string ComponentId { get; set; } = string.Empty;

    /// <summary>Gets or sets the slot receiving the calculated result.</summary>
    [JsonRequired]
    public string OutputSlotId { get; set; } = string.Empty;

    /// <summary>Gets or sets the bounded formula tree.</summary>
    [JsonRequired]
    public VisualBriefingFormulaNode Formula { get; set; } = new();
}