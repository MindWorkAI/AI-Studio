using System.Globalization;

using AIStudio.Components;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Chat;

public partial class ChartBlock : MSGComponentBase
{
    private AxisChartOptions AxisChartOptions { get; } = new() { MatchBoundsToSize = true };

    private ChartOptions ChartOptions { get; } = new()
    {
        ChartPalette =
        [
            "#236A50", "#F2D264", "#79AE90", "#C97857", "#4E7894", "#9B6B8F",
            "#6A233D", "#6484F2", "#AE7997", "#57A8C9", "#946A4E", "#6B9B77",
        ],
    };

    private ChartOptions HeatMapChartOptions { get; } = new()
    {
        ChartPalette = ["#236A50", "#79AE90", "#F2D264", "#C97857", "#6A233D"],
        EnableSmoothGradient = true,
        YAxisLabelPosition = YAxisLabelPosition.Right,
        
    };

    [Parameter]
    public ChartBlockParseResult Result { get; set; } = ChartBlockParseResult.Invalid(string.Empty, string.Empty);

    private ChartType ChartType => this.Result.Chart?.Type switch
    {
        ChartDefinitionType.BAR => ChartType.Bar,
        ChartDefinitionType.STACKED_BAR => ChartType.StackedBar,
        ChartDefinitionType.LINE => ChartType.Line,
        ChartDefinitionType.PIE => ChartType.Pie,
        ChartDefinitionType.DONUT => ChartType.Donut,
        ChartDefinitionType.HEATMAP => ChartType.HeatMap,
        _ => ChartType.Bar,
    };

    private List<ChartSeries> ChartSeries => this.Result.Chart?.Series
        .Select(series => new ChartSeries { Name = series.Name, Data = series.Values.ToArray() })
        .ToList() ?? [];

    private ChartOptions CategoryChartOptions => this.Result.Chart?.Type is ChartDefinitionType.HEATMAP
        ? this.HeatMapChartOptions
        : this.ChartOptions;

    private List<TimeSeriesChartSeries> TimeSeriesChartSeries => this.Result.Chart is not { } chart
        ? []
        : chart.Series
            .Select(series => new TimeSeriesChartSeries
            {
                Name = series.Name,
                Data = chart.Categories
                    .Select((category, index) => new TimeSeriesChartSeries.TimeValue(
                        DateTimeOffset.Parse(category, CultureInfo.InvariantCulture).UtcDateTime,
                        series.Values[index]))
                    .ToList(),
                IsVisible = true,
            })
            .ToList();

    private TimeSpan TimeLabelSpacing
    {
        get
        {
            var timestamps = this.GetTimeSeriesTimestamps();
            if (timestamps.Count < 2)
                return TimeSpan.FromSeconds(1);

            var range = timestamps[^1] - timestamps[0];
            var intervalCount = Math.Min(timestamps.Count - 1, 8);
            return TimeSpan.FromTicks(Math.Max(TimeSpan.TicksPerSecond, range.Ticks / intervalCount));
        }
    }

    private string TimeLabelFormat
    {
        get
        {
            var timestamps = this.GetTimeSeriesTimestamps();
            if (timestamps.Count < 2)
                return "yyyy-MM-dd HH:mm";

            var range = timestamps[^1] - timestamps[0];
            if (range <= TimeSpan.FromDays(2))
                return "MM-dd HH:mm";

            return range <= TimeSpan.FromDays(730) ? "yyyy-MM-dd" : "yyyy";
        }
    }

    private List<DateTimeOffset> GetTimeSeriesTimestamps() => this.Result.Chart?.Categories
        .Select(category => DateTimeOffset.Parse(category, CultureInfo.InvariantCulture).ToUniversalTime())
        .ToList() ?? [];
}
