using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// Shows a complete SWOT analysis, in the order of SwotAnalysisMarkdownFormatter: the matrix, the
/// overall assessment, the prioritized actions, the details of each category, and what the moderator
/// changed.
/// </summary>
public partial class SwotAnalysisResultView
{
    /// <summary>
    /// The analysis to show.
    /// </summary>
    [Parameter]
    public SwotAnalysisResult Result { get; set; } = new();

    private SwotQuadrantLabels Labels => new(
        this.Result.Strengths.Label,
        this.Result.Weaknesses.Label,
        this.Result.Opportunities.Label,
        this.Result.Threats.Label,
        this.Result.InternalLabel,
        this.Result.ExternalLabel,
        this.Result.PositiveLabel,
        this.Result.NegativeLabel);

    private SwotCategory CategoryOf(SwotAnalysisCategory category) => category switch
    {
        SwotAnalysisCategory.STRENGTHS => this.Result.Strengths,
        SwotAnalysisCategory.WEAKNESSES => this.Result.Weaknesses,
        SwotAnalysisCategory.OPPORTUNITIES => this.Result.Opportunities,
        SwotAnalysisCategory.THREATS => this.Result.Threats,

        _ => new(),
    };
}