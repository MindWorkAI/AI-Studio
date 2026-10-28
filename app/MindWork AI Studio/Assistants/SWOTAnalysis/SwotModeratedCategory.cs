using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// A SWOT category as the moderator returns it.
/// </summary>
internal sealed class SwotModeratedCategory
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("empty_message")]
    public string EmptyMessage { get; set; } = string.Empty;

    [JsonPropertyName("findings")]
    public List<SwotModeratedFinding> Findings { get; set; } = [];
}