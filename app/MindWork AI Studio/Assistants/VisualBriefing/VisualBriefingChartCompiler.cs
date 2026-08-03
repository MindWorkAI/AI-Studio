using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Turns a validated chart specification into a branded chart-library option object.
/// </summary>
internal static class VisualBriefingChartCompiler
{
    /// <summary>
    /// Compiles one validated chart specification into an Apache ECharts option object.
    /// </summary>
    /// <param name="chart">The validated chart specification.</param>
    /// <returns>The branded chart option.</returns>
    internal static JsonElement Compile(VisualBriefingChartSpec chart)
    {
        object series = chart.Kind switch
        {
            VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT =>
                chart.Categories.Select((category, index) => new
                {
                    name = category,
                    value = chart.Series[0].Values[index],
                }).ToArray(),

            VisualBriefingChartKind.RADAR => chart.Series.Select(item => new
            {
                name = item.Name,
                type = "radar",
                data = new[]
                {
                    new
                    {
                        value = item.Values,
                        name = item.Name,
                    },
                },
            }).ToArray(),

            _ => chart.Series.Select(item => new
            {
                name = item.Name,
                type = SeriesType(chart.Kind),
                stack = chart.Kind is VisualBriefingChartKind.STACKED_BAR ? "total" : null,
                areaStyle = chart.Kind is VisualBriefingChartKind.AREA ? new { opacity = 0.18 } : null,
                smooth = chart.Kind is VisualBriefingChartKind.LINE or VisualBriefingChartKind.AREA,
                showSymbol = chart.Kind is VisualBriefingChartKind.SCATTER,
                symbolSize = chart.Kind is VisualBriefingChartKind.SCATTER ? 10 : 6,
                itemStyle = chart.Kind is VisualBriefingChartKind.BAR or VisualBriefingChartKind.STACKED_BAR
                    ? new { borderRadius = new[] { 6, 6, 0, 0 } } : null,
                data = item.Values,
            }).ToArray(),
        };

        var option = new
        {
            color = new[] { "#236A50", "#F2D264", "#79AE90", "#C97857", "#4E7894", "#9B6B8F" },
            backgroundColor = "transparent",
            textStyle = new
            {
                color = "#172A24",
                fontFamily = "system-ui, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif",
            },

            tooltip = new
            {
                trigger = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT ? "item" : "axis",
                borderColor = "#D6E2DC",
                backgroundColor = "#FFFEFA",
                textStyle = new { color = "#172A24" },
            },

            legend = new { show = true, top = 0, textStyle = new { color = "#4F635B" } },
            grid = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT or VisualBriefingChartKind.RADAR
                ? null
                : new { left = 8, right = 16, top = 48, bottom = 8, containLabel = true },

            xAxis = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT or VisualBriefingChartKind.RADAR
                ? null
                : new
                {
                    type = "category",
                    data = chart.Categories,
                    axisLine = new { lineStyle = new { color = "#B8C9C0" } },
                    axisTick = new { show = false },
                    axisLabel = new { color = "#5E7169" },
                },

            yAxis = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT or VisualBriefingChartKind.RADAR
                ? null
                : new
                {
                    type = "value",
                    axisLine = new { show = false },
                    axisTick = new { show = false },
                    axisLabel = new { color = "#5E7169" },
                    splitLine = new { lineStyle = new { color = "#E1EAE5" } },
                },

            radar = chart.Kind is VisualBriefingChartKind.RADAR
                ? new
                {
                    indicator = chart.Categories.Select(name => new { name }).ToArray(),
                    splitArea = new { areaStyle = new { color = new[] { "#FFFEFA", "#EAF1EC" } } },
                    axisName = new { color = "#5E7169" },
                    splitLine = new { lineStyle = new { color = "#B8C9C0" } },
                }
                : null,

            series = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT
                ? new[]
                {
                    new
                    {
                        type = "pie",
                        radius = chart.Kind is VisualBriefingChartKind.DONUT
                            ? new[] { "45%", "70%" }
                            : new[] { "0%", "70%" },
                        padAngle = 2,
                        itemStyle = new { borderColor = "#FFFEFA", borderWidth = 2, borderRadius = 5 },
                        label = new { color = "#4F635B" },
                        data = series,
                    },
                }
                : series,
        };

        return JsonSerializer.SerializeToElement(option, VisualBriefingJson.Canonical);
    }

    /// <summary>
    /// Maps a semantic chart kind to its Apache ECharts series type.
    /// </summary>
    /// <param name="kind">The semantic chart kind.</param>
    /// <returns>The Apache ECharts series type.</returns>
    private static string SeriesType(VisualBriefingChartKind kind) => kind switch
    {
        VisualBriefingChartKind.LINE or VisualBriefingChartKind.AREA => "line",
        VisualBriefingChartKind.BAR or VisualBriefingChartKind.STACKED_BAR => "bar",
        VisualBriefingChartKind.SCATTER => "scatter",
        VisualBriefingChartKind.RADAR => "radar",
        _ => "line",
    };
}