using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How fast the models answered: the median times as a chart, and as a table which also lists the models without a
/// complete answer.
/// </summary>
public partial class ModelComparisonSpeeds : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the speeds of all models.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ModelComparisonModelSpeed> Speeds { get; set; } = [];

    /// <summary>
    /// The models with complete answers, whose median times the chart can show.
    /// </summary>
    private IReadOnlyList<ModelComparisonModelSpeed> timedSpeeds = [];

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        this.timedSpeeds = [.. this.Speeds.Where(speed => speed is { MedianFirstTokenTime: not null, MedianTotalTime: not null })];
        base.OnParametersSet();
    }

    #endregion

    /// <summary>
    /// The median times of each model in seconds, as the two series of a bar chart.
    /// </summary>
    private List<ChartSeries<double>> SpeedSeries =>
    [
        new() { Name = T("Until the first token"), Data = new([.. this.timedSpeeds.Select(speed => speed.MedianFirstTokenTime!.Value.TotalSeconds)]) },
        new() { Name = T("Until the complete answer"), Data = new([.. this.timedSpeeds.Select(speed => speed.MedianTotalTime!.Value.TotalSeconds)]) },
    ];

    private BarChartOptions ChartOptions => new()
    {
        ChartPalette = ModelComparisonCharts.SPEED_PALETTE,
        YAxisTicks = ModelComparisonCharts.AxisStep(this.timedSpeeds.Select(speed => speed.MedianTotalTime!.Value.TotalSeconds)),
        YAxisToStringFunc = ModelComparisonFormat.Seconds,
        XAxisLabelRotation = ModelComparisonCharts.LabelRotation(this.timedSpeeds.Count),
    };
}