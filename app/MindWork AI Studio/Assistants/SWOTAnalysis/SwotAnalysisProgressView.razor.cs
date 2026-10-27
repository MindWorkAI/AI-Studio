using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// Shows how far the analysis of each SWOT category is, and its findings as soon as they are there.
/// </summary>
public partial class SwotAnalysisProgressView : MSGComponentBase
{
    /// <summary>
    /// The progress per category; a category without one counts as running.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<SwotAnalysisCategory, SwotCategoryAnalysisProgress> Progress { get; set; } = null!;

    private static readonly SwotCategoryAnalysisProgress RUNNING = new(SwotCategoryAnalysisStatus.RUNNING);

    private SwotCategoryAnalysisProgress ProgressOf(SwotAnalysisCategory category) => this.Progress.GetValueOrDefault(category, RUNNING);

    private string LabelOf(SwotAnalysisCategory category) => category switch
    {
        SwotAnalysisCategory.STRENGTHS => T("Strengths"),
        SwotAnalysisCategory.WEAKNESSES => T("Weaknesses"),
        SwotAnalysisCategory.OPPORTUNITIES => T("Opportunities"),
        SwotAnalysisCategory.THREATS => T("Threats"),

        _ => string.Empty,
    };
}