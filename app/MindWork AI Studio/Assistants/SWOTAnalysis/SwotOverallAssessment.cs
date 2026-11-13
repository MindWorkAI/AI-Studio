using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The overall assessment of a SWOT analysis, which weighs the four categories against each other.
/// </summary>
public sealed class SwotOverallAssessment
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The paragraphs of the text, one per line the model wrote.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> Paragraphs => this.Text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}