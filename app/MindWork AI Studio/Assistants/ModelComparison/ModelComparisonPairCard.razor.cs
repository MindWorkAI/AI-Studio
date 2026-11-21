using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How two models did against each other, by the votes of the user and, when there are any, by the verdicts of the
/// judges: as a chart, and as a table with the assessment.
/// </summary>
public partial class ModelComparisonPairCard : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the pair.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonPairing Pairing { get; set; } = null!;

    /// <summary>
    /// Gets or sets what the line below the names says, or null for the number of stored comparisons.
    /// </summary>
    [Parameter]
    public string? Caption { get; set; }

    /// <summary>
    /// The tallies the chart shows: the votes of the user, and the verdicts of the judges when there are any.
    /// </summary>
    private IReadOnlyList<ModelComparisonTally> Tallies => this.Pairing.JudgeVerdicts.ScoredRunCount > 0
        ? [this.Pairing.Votes, this.Pairing.JudgeVerdicts]
        : [this.Pairing.Votes];

    private string[] ChartLabels => this.Pairing.JudgeVerdicts.ScoredRunCount > 0
        ? [TA("Your votes"), T("Judge verdicts")]
        : [TA("Your votes")];

    /// <summary>
    /// The wins of the first model, the ties, and the wins of the second model, as the three series of a stacked bar
    /// chart with one bar per tally, in the order of the columns of the table.
    /// </summary>
    private List<ChartSeries<double>> OutcomeSeries =>
    [
        new() { Name = this.Pairing.First.Label, Data = new([.. this.Tallies.Select(tally => (double)tally.FirstModelWins)]) },
        new() { Name = T("Equally good"), Data = new([.. this.Tallies.Select(tally => (double)tally.Ties)]) },
        new() { Name = this.Pairing.Second.Label, Data = new([.. this.Tallies.Select(tally => (double)tally.SecondModelWins)]) },
    ];

    private StackedBarChartOptions ChartOptions => new()
    {
        ChartPalette = ModelComparisonCharts.PAIR_PALETTE,
        YAxisTicks = ModelComparisonCharts.AxisStep(this.Tallies.Select(tally => (double)(tally.FirstModelWins + tally.Ties + tally.SecondModelWins))),
        YAxisToStringFunc = value => ((long)value).CompactCount(),
    };

    private string CaptionText => this.Caption ?? string.Format(ModelComparisonFormat.Culture, T("Stored comparisons: {0}"), this.Pairing.ComparisonCount.CompactCount());

    /// <summary>
    /// Says which model of the pair won clearly, if one did.
    /// </summary>
    /// <param name="winShareOfFirst">The share of wins of the first model, by the votes or by the judges.</param>
    private string SignificanceText(ModelComparisonWinShare winShareOfFirst) => winShareOfFirst.Significance switch
    {
        ModelComparisonSignificance.CLEARLY_BETTER => string.Format(T("{0} is clearly better"), this.Pairing.First.Label),
        ModelComparisonSignificance.CLEARLY_WORSE => string.Format(T("{0} is clearly better"), this.Pairing.Second.Label),
        _ => T("Not yet clear"),
    };
}