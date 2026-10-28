using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The prioritized actions as the moderator returns them.
/// </summary>
internal sealed class SwotModerationActions
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("empty_message")]
    public string EmptyMessage { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<SwotModerationAction> Items { get; set; } = [];
}