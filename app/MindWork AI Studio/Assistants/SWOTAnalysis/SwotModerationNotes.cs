using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// What the moderator changed in the four categories, and why.
/// </summary>
public sealed class SwotModerationNotes
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("empty_message")]
    public string EmptyMessage { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<SwotModerationNote> Items { get; set; } = [];
}