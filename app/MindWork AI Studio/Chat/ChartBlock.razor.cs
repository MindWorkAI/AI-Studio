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

    [Parameter]
    public ChartBlockParseResult Result { get; set; } = ChartBlockParseResult.Invalid(string.Empty, string.Empty);

    private ChartType ChartType => this.Result.Chart?.Type switch
    {
        ChartDefinitionType.BAR => ChartType.Bar,
        ChartDefinitionType.STACKED_BAR => ChartType.StackedBar,
        ChartDefinitionType.LINE => ChartType.Line,
        ChartDefinitionType.PIE => ChartType.Pie,
        ChartDefinitionType.DONUT => ChartType.Donut,
        _ => ChartType.Bar,
    };

    private List<ChartSeries> ChartSeries => this.Result.Chart?.Series
        .Select(series => new ChartSeries { Name = series.Name, Data = series.Values.ToArray() })
        .ToList() ?? [];
}
