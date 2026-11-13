using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// A ranking of the models: their wins, ties, and losses as a chart, and as a table with their share of wins and how
/// sure that share is.
/// </summary>
public partial class ModelComparisonRanking : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the models, best first, by the votes of the user or by the verdicts of the judges.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ModelComparisonModelStanding> Standings { get; set; } = [];

    /// <summary>
    /// The wins, ties, and losses of each model, as the three series of a stacked bar chart.
    /// </summary>
    private List<ChartSeries<double>> OutcomeSeries =>
    [
        new() { Name = T("Wins"), Data = new([.. this.Standings.Select(standing => (double)standing.Wins)]) },
        new() { Name = T("Ties"), Data = new([.. this.Standings.Select(standing => (double)standing.Ties)]) },
        new() { Name = T("Losses"), Data = new([.. this.Standings.Select(standing => (double)standing.Losses)]) },
    ];

    private StackedBarChartOptions ChartOptions => new()
    {
        ChartPalette = ModelComparisonCharts.OUTCOME_PALETTE,
        YAxisTicks = ModelComparisonCharts.AxisStep(this.Standings.Select(standing => (double)(standing.Wins + standing.Ties + standing.Losses))),
        YAxisToStringFunc = value => ((long)value).CompactCount(),
        XAxisLabelRotation = ModelComparisonCharts.LabelRotation(this.Standings.Count),
    };

    private string FormatRange(ModelComparisonWinShare winShare) => winShare is { LowerBound: { } lower, UpperBound: { } upper }
        ? string.Format(ModelComparisonFormat.Culture, T("{0} to {1}"), ModelComparisonFormat.Share(lower), ModelComparisonFormat.Share(upper))
        : "-";

    private string SignificanceText(ModelComparisonSignificance significance) => significance switch
    {
        ModelComparisonSignificance.CLEARLY_BETTER => T("Clearly better"),
        ModelComparisonSignificance.CLEARLY_WORSE => T("Clearly worse"),
        _ => T("Not yet clear"),
    };
}