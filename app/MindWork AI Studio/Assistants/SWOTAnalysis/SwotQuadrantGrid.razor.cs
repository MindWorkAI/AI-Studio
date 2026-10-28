using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// Places the four SWOT quadrants side by side, or below each other when the space is too narrow.
/// </summary>
/// <remarks>
/// Each quadrant shows its name, where it lies on both axes, and its color, so the live progress and
/// the result look the same. The grid decides by the width it has, not by the width of the window:
/// the assistant and the chat give it very different widths.
/// </remarks>
public partial class SwotQuadrantGrid
{
    /// <summary>
    /// The quadrants in the order of the matrix: the internal ones first, the positive ones left.
    /// </summary>
    private static readonly SwotAnalysisCategory[] CATEGORIES = Enum.GetValues<SwotAnalysisCategory>();

    /// <summary>
    /// The names of the quadrants and the ends of the axes.
    /// </summary>
    [Parameter, EditorRequired]
    public SwotQuadrantLabels Labels { get; set; } = null!;

    /// <summary>
    /// What a quadrant shows below its name.
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