using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// A finding as the moderator returns it, with the factor ID the prioritized actions refer to.
/// </summary>
internal sealed class SwotModeratedFinding
{
    [JsonPropertyName("factor_id")]
    public string FactorId { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;
}