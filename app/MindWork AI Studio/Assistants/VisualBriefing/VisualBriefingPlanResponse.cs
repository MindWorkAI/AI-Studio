using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines the strict structured response returned by the plan agent.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingPlanResponse
{
    /// <summary>Gets or sets the plan contract version.</summary>
    [JsonRequired]
    public int ContractVersion { get; set; }

    /// <summary>Gets or sets the ordered briefing sections.</summary>
    [JsonRequired]
    public List<VisualBriefingPlanSection> Sections { get; set; } = [];
}