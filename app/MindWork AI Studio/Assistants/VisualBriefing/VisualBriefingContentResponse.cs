using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines the strict structured response returned by the content agent.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingContentResponse
{
    /// <summary>Gets or sets the content contract version.</summary>
    [JsonRequired]
    public int ContractVersion { get; set; }

    /// <summary>Gets or sets exactly one value for every planned slot.</summary>
    [JsonRequired]
    public List<VisualBriefingSlotValue> Slots { get; set; } = [];

    /// <summary>Gets or sets the semantic chart specifications.</summary>
    [JsonRequired]
    public List<VisualBriefingChartSpec> Charts { get; set; } = [];

    /// <summary>Gets or sets the declarative interaction controls.</summary>
    [JsonRequired]
    public List<VisualBriefingControlSpec> Controls { get; set; } = [];

    /// <summary>Gets or sets the deterministic simulation formulas.</summary>
    [JsonRequired]
    public List<VisualBriefingFormulaSpec> Formulas { get; set; } = [];

    /// <summary>Gets or sets assistive descriptions keyed by component identifier.</summary>
    [JsonRequired]
    public Dictionary<string, string> AccessibilityTexts { get; set; } = new(StringComparer.Ordinal);
}