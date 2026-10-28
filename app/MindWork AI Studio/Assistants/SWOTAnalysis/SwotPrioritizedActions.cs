using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

public sealed class SwotPrioritizedActions
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("empty_message")]
    public string EmptyMessage { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<SwotAction> Items { get; set; } = [];
}