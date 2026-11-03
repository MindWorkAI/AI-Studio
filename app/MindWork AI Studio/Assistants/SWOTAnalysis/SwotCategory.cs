using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

public sealed class SwotCategory
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("empty_message")]
    public string EmptyMessage { get; set; } = string.Empty;

    [JsonPropertyName("findings")]
    public List<SwotFinding> Findings { get; set; } = [];
}