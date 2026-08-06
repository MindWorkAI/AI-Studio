using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines the strict structured response returned by the design agent.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingDesignResponse
{
    /// <summary>Gets or sets the design contract version.</summary>
    [JsonRequired]
    public int ContractVersion { get; set; }

    /// <summary>Gets or sets the bounded MindWork design profile.</summary>
    [JsonRequired]
    public VisualBriefingDesignProfile Profile { get; set; }

    /// <summary>Gets or sets the validated presentation layout.</summary>
    [JsonRequired]
    public VisualBriefingLayoutNode Layout { get; set; } = new();
}