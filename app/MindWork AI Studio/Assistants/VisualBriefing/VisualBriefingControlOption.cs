using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines one value and visible label offered by an interaction control.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VisualBriefingControlOption
{
    /// <summary>Gets or sets the stored option value.</summary>
    [JsonRequired]
    public string Value { get; init; } = string.Empty;

    /// <summary>Gets or sets the visible option label.</summary>
    [JsonRequired]
    public string Label { get; init; } = string.Empty;
}