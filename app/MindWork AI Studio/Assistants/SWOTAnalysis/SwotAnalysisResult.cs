using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The complete SWOT analysis, as the result view shows it and the chat stores it.
/// </summary>
/// <remarks>
/// The assistant keeps its result in its session state, which compares objects by reference since
/// the mirror instance takes over only what changed. Therefore, replace a result as a whole and never
/// change it in place: a change in place would not reach an instance the user returns to.
/// </remarks>
public sealed class SwotAnalysisResult
{
    [JsonPropertyName("matrix_heading")]
    public string MatrixHeading { get; set; } = string.Empty;

    [JsonPropertyName("internal_label")]
    public string InternalLabel { get; set; } = string.Empty;

    [JsonPropertyName("external_label")]
    public string ExternalLabel { get; set; } = string.Empty;

    [JsonPropertyName("positive_label")]
    public string PositiveLabel { get; set; } = string.Empty;

    [JsonPropertyName("negative_label")]
    public string NegativeLabel { get; set; } = string.Empty;

    [JsonPropertyName("strengths")]
    public SwotCategory Strengths { get; set; } = new();

    [JsonPropertyName("weaknesses")]
    public SwotCategory Weaknesses { get; set; } = new();

    [JsonPropertyName("opportunities")]
    public SwotCategory Opportunities { get; set; } = new();

    [JsonPropertyName("threats")]
    public SwotCategory Threats { get; set; } = new();

    [JsonPropertyName("overall_assessment")]
    public SwotOverallAssessment OverallAssessment { get; set; } = new();

    [JsonPropertyName("prioritized_actions")]
    public SwotPrioritizedActions PrioritizedActions { get; set; } = new();

    [JsonPropertyName("moderation_notes")]
    public SwotModerationNotes ModerationNotes { get; set; } = new();

    [JsonIgnore]
    public IReadOnlyList<SwotCategory> Categories =>
    [
        this.Strengths,
        this.Weaknesses,
        this.Opportunities,
        this.Threats,
    ];
}