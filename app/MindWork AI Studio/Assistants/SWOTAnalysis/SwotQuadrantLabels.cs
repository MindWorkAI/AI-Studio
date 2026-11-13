namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The texts of the quadrant grid: the name of each quadrant and both ends of both axes.
/// </summary>
/// <param name="Strengths">The name of the strengths.</param>
/// <param name="Weaknesses">The name of the weaknesses.</param>
/// <param name="Opportunities">The name of the opportunities.</param>
/// <param name="Threats">The name of the threats.</param>
/// <param name="Internal">The internal end of the vertical axis.</param>
/// <param name="External">The external end of the vertical axis.</param>
/// <param name="Positive">The positive end of the horizontal axis.</param>
/// <param name="Negative">The negative end of the horizontal axis.</param>
public sealed record SwotQuadrantLabels(
    string Strengths,
    string Weaknesses,
    string Opportunities,
    string Threats,
    string Internal,
    string External,
    string Positive,
    string Negative)
{
    public string NameOf(SwotAnalysisCategory category) => category switch
    {
        SwotAnalysisCategory.STRENGTHS => this.Strengths,
        SwotAnalysisCategory.WEAKNESSES => this.Weaknesses,
        SwotAnalysisCategory.OPPORTUNITIES => this.Opportunities,
        SwotAnalysisCategory.THREATS => this.Threats,

        _ => string.Empty,
    };

    /// <summary>
    /// Where a quadrant lies on both axes, e.g., "internal · positive".
    /// </summary>
    /// <remarks>
    /// A grid with two columns shows the axes by the position of its quadrants, a single column does
    /// not; this line keeps the position readable in both.
    /// </remarks>
    public string AxesOf(SwotAnalysisCategory category) => category switch
    {
        SwotAnalysisCategory.STRENGTHS => $"{this.Internal} · {this.Positive}",
        SwotAnalysisCategory.WEAKNESSES => $"{this.Internal} · {this.Negative}",
        SwotAnalysisCategory.OPPORTUNITIES => $"{this.External} · {this.Positive}",
        SwotAnalysisCategory.THREATS => $"{this.External} · {this.Negative}",

        _ => string.Empty,
    };
}