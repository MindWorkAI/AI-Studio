using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// Lists the summaries of the findings of one SWOT category, or why there are none.
/// </summary>
public partial class SwotMatrixFindings
{
    /// <summary>
    /// The findings of the category.
    /// </summary>
    [Parameter]
    public IReadOnlyList<SwotFinding> Findings { get; set; } = [];

    /// <summary>
    /// Why the category has no findings.
    /// </summary>
    [Parameter]
    public string EmptyMessage { get; set; } = string.Empty;
}