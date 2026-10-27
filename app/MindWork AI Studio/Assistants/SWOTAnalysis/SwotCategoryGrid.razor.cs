using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// Places the four SWOT categories in a grid, each in the color of its category.
/// </summary>
public partial class SwotCategoryGrid
{
    /// <summary>
    /// The categories in the order of the matrix: the internal ones first, the positive ones left.
    /// </summary>
    private static readonly SwotAnalysisCategory[] CATEGORIES = Enum.GetValues<SwotAnalysisCategory>();

    /// <summary>
    /// What a category shows.
    /// </summary>
    [Parameter, EditorRequired]
    public RenderFragment<SwotAnalysisCategory> ChildContent { get; set; } = null!;

    private static string CssClassOf(SwotAnalysisCategory category) => category switch
    {
        SwotAnalysisCategory.STRENGTHS => "swot-strengths",
        SwotAnalysisCategory.WEAKNESSES => "swot-weaknesses",
        SwotAnalysisCategory.OPPORTUNITIES => "swot-opportunities",
        SwotAnalysisCategory.THREATS => "swot-threats",

        _ => string.Empty,
    };
}