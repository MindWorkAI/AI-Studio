using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The effort with each model of a comparison series as two bar charts: how many messages the user wrote on average,
/// and the median time the user worked with it. A model without a value is left out of that chart.
/// </summary>
public partial class ModelComparisonEffortCharts : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the effort with each model, in the order of the models of the series.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ModelComparisonSeriesEffort> Efforts { get; set; } = [];

    /// <summary>
    /// The efforts with an average of the messages.
    /// </summary>
    private List<ModelComparisonSeriesEffort> messageEfforts = [];

    /// <summary>
    /// The efforts with a median time.
    /// </summary>
    private List<ModelComparisonSeriesEffort> timeEfforts = [];

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        this.messageEfforts = [.. this.Efforts.Where(effort => effort.AverageUserMessages is not null)];
        this.timeEfforts = [.. this.Efforts.Where(effort => effort.MedianDuration is not null)];
        base.OnParametersSet();
    }

    #endregion

    private List<ChartSeries<double>> MessageSeries =>
    [
        new() { Name = TA("Your messages on average"), Data = new([.. this.messageEfforts.Select(effort => effort.AverageUserMessages!.Value)]) },
    ];

    private List<ChartSeries<double>> TimeSeries =>
    [
        new() { Name = T("Median time"), Data = new([.. this.timeEfforts.Select(effort => effort.MedianDuration!.Value.TotalMinutes)]) },
    ];

    /// <summary>
    /// A single series needs no legend: the title above the chart names it.
    /// </summary>
    private BarChartOptions MessageChartOptions => new()
    {
        ChartPalette = ModelComparisonCharts.SINGLE_PALETTE,
        ShowLegend = false,
        YAxisTicks = ModelComparisonCharts.AxisStep(this.messageEfforts.Select(effort => effort.AverageUserMessages!.Value)),
        YAxisToStringFunc = value => ((long)value).CompactCount(),
        XAxisLabelRotation = ModelComparisonCharts.LabelRotation(this.messageEfforts.Count),
    };

    private BarChartOptions TimeChartOptions => new()
    {
        ChartPalette = ModelComparisonCharts.SINGLE_PALETTE,
        ShowLegend = false,
        YAxisTicks = ModelComparisonCharts.AxisStep(this.timeEfforts.Select(effort => effort.MedianDuration!.Value.TotalMinutes)),
        YAxisToStringFunc = value => ModelComparisonFormat.Minutes(TimeSpan.FromMinutes(value)),
        XAxisLabelRotation = ModelComparisonCharts.LabelRotation(this.timeEfforts.Count),
    };
}