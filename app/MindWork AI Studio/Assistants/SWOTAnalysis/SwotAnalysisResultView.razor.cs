using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// Shows a complete SWOT analysis: the matrix first, then the details and the prioritized actions.
/// </summary>
public partial class SwotAnalysisResultView
{
    /// <summary>
    /// The analysis to show.
    /// </summary>
    [Parameter]
    public SwotAnalysisResult Result { get; set; } = new();
}