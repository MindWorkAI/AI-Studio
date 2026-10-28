using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The answer of the moderator: the reconciled categories, what changed, the overall assessment, and
/// the prioritized actions.
/// </summary>
internal sealed class SwotModerationResult
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
    public SwotModeratedCategory Strengths { get; set; } = new();

    [JsonPropertyName("weaknesses")]
    public SwotModeratedCategory Weaknesses { get; set; } = new();

    [JsonPropertyName("opportunities")]
    public SwotModeratedCategory Opportunities { get; set; } = new();

    [JsonPropertyName("threats")]
    public SwotModeratedCategory Threats { get; set; } = new();

    [JsonPropertyName("moderation_notes")]
    public SwotModerationNotes ModerationNotes { get; set; } = new();

    [JsonPropertyName("overall_assessment")]
    public SwotOverallAssessment OverallAssessment { get; set; } = new();

    [JsonPropertyName("prioritized_actions")]
    public SwotModerationActions PrioritizedActions { get; set; } = new();

    public SwotModeratedCategory CategoryOf(SwotAnalysisCategory category) => category switch
    {
        SwotAnalysisCategory.STRENGTHS => this.Strengths,
        SwotAnalysisCategory.WEAKNESSES => this.Weaknesses,
        SwotAnalysisCategory.OPPORTUNITIES => this.Opportunities,
        SwotAnalysisCategory.THREATS => this.Threats,

        _ => new(),
    };
}