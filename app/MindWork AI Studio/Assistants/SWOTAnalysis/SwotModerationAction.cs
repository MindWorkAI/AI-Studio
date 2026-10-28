using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// A prioritized action as the moderator returns it, referring to findings by their factor IDs.
/// </summary>
/// <remarks>
/// IDs instead of the summaries: a model which repeats a long summary changes it now and then, and
/// the reference would no longer match. The assembly replaces the IDs with the summaries.
/// </remarks>
internal sealed class SwotModerationAction
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;

    [JsonPropertyName("addressed_factor_ids")]
    public List<string> AddressedFactorIds { get; set; } = [];
}