using AIStudio.Components;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Chat;

public partial class ChartBlock : MSGComponentBase
{
    private AxisChartOptions AxisChartOptions { get; } = new() { MatchBoundsToSize = true };

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
