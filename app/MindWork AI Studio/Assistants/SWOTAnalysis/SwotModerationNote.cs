using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// One change of the moderator to the four categories.
/// </summary>
public sealed class SwotModerationNote
{
    [JsonPropertyName("change")]
    public string Change { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}