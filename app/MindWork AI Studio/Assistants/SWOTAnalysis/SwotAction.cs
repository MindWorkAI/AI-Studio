using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

public sealed class SwotAction
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;

    [JsonPropertyName("addressed_factors")]
    public List<string> AddressedFactors { get; set; } = [];
}