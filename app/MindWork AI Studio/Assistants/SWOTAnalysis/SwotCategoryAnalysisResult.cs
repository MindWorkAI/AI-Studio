using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

internal sealed class SwotCategoryAnalysisResult
{
    [JsonPropertyName("empty_message")]
    public string EmptyMessage { get; set; } = string.Empty;

    [JsonPropertyName("findings")]
    public List<SwotFinding> Findings { get; set; } = [];
}