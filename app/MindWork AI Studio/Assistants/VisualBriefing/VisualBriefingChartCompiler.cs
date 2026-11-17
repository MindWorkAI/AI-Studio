using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Turns a validated chart specification into a branded chart-library option object.
/// </summary>
/// <remarks>
/// The option object is built as JSON nodes rather than as types of its own: it follows the options of Apache
/// ECharts, which differ for every kind of chart, and nothing in AI Studio reads it again. The order of its
/// properties is part of the compiled data, which gets hashed.
/// </remarks>
internal static class VisualBriefingChartCompiler
{
    /// <summary>
    /// Compiles one validated chart specification into an Apache ECharts option object.
    /// </summary>
    /// <param name="chart">The validated chart specification.</param>
    /// <returns>The branded chart option.</returns>
    internal static JsonElement Compile(VisualBriefingChartSpec chart)
    {
        var series = chart.Kind switch
        {
            VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT => new JsonArray(
            [
                .. chart.Categories.Select((category, index) => new JsonObject
                {
                    ["name"] = category,
                    ["value"] = chart.Series[0].Values[index],
                }),
            ]),

            VisualBriefingChartKind.RADAR => new JsonArray(
            [
                .. chart.Series.Select(item => new JsonObject
                {
                    ["name"] = item.Name,
                    ["type"] = "radar",
                    ["data"] = new JsonArray(new JsonObject
                    {
                        ["value"] = Numbers(item.Values),
                        ["name"] = item.Name,
                    }),
                }),
            ]),

            _ => new JsonArray(
            [
                .. chart.Series.Select(item => new JsonObject
                {
                    ["name"] = item.Name,
                    ["type"] = SeriesType(chart.Kind),
                    ["stack"] = chart.Kind is VisualBriefingChartKind.STACKED_BAR ? "total" : null,
                    ["areaStyle"] = chart.Kind is VisualBriefingChartKind.AREA ? new JsonObject { ["opacity"] = 0.18 } : null,
                    ["smooth"] = chart.Kind is VisualBriefingChartKind.LINE or VisualBriefingChartKind.AREA,
                    ["showSymbol"] = chart.Kind is VisualBriefingChartKind.SCATTER,
                    ["symbolSize"] = chart.Kind is VisualBriefingChartKind.SCATTER ? 10 : 6,
                    ["itemStyle"] = chart.Kind is VisualBriefingChartKind.BAR or VisualBriefingChartKind.STACKED_BAR
                        ? new JsonObject { ["borderRadius"] = new JsonArray(6, 6, 0, 0) } : null,
                    ["data"] = Numbers(item.Values),
                }),
            ]),
        };

        var isCircular = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT;
        var hasAxes = chart.Kind is not (VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT or VisualBriefingChartKind.RADAR);
        var option = new JsonObject
        {
            ["color"] = Texts("#236A50", "#F2D264", "#79AE90", "#C97857", "#4E7894", "#9B6B8F"),
            ["backgroundColor"] = "transparent",
            ["textStyle"] = new JsonObject
            {
                ["color"] = "#172A24",
                ["fontFamily"] = "system-ui, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif",
            },

            ["tooltip"] = new JsonObject
            {
                ["trigger"] = isCircular ? "item" : "axis",
                ["borderColor"] = "#D6E2DC",
                ["backgroundColor"] = "#FFFEFA",
                ["textStyle"] = new JsonObject { ["color"] = "#172A24" },
            },

            ["legend"] = new JsonObject { ["show"] = true, ["top"] = 0, ["textStyle"] = new JsonObject { ["color"] = "#4F635B" } },
            ["grid"] = hasAxes
                ? new JsonObject { ["left"] = 8, ["right"] = 16, ["top"] = 48, ["bottom"] = 8, ["containLabel"] = true }
                : null,

            ["xAxis"] = hasAxes
                ? new JsonObject
                {
                    ["type"] = "category",
                    ["data"] = Texts(chart.Categories),
                    ["axisLine"] = new JsonObject { ["lineStyle"] = new JsonObject { ["color"] = "#B8C9C0" } },
                    ["axisTick"] = new JsonObject { ["show"] = false },
                    ["axisLabel"] = new JsonObject { ["color"] = "#5E7169" },
                }
                : null,

            ["yAxis"] = hasAxes
                ? new JsonObject
                {
                    ["type"] = "value",
                    ["axisLine"] = new JsonObject { ["show"] = false },
                    ["axisTick"] = new JsonObject { ["show"] = false },
                    ["axisLabel"] = new JsonObject { ["color"] = "#5E7169" },
                    ["splitLine"] = new JsonObject { ["lineStyle"] = new JsonObject { ["color"] = "#E1EAE5" } },
                }
                : null,

            ["radar"] = chart.Kind is VisualBriefingChartKind.RADAR
                ? new JsonObject
                {
                    ["indicator"] = new JsonArray([.. chart.Categories.Select(name => new JsonObject { ["name"] = name })]),
                    ["splitArea"] = new JsonObject { ["areaStyle"] = new JsonObject { ["color"] = Texts("#FFFEFA", "#EAF1EC") } },
                    ["axisName"] = new JsonObject { ["color"] = "#5E7169" },
                    ["splitLine"] = new JsonObject { ["lineStyle"] = new JsonObject { ["color"] = "#B8C9C0" } },
                }
                : null,

            ["series"] = isCircular
                ? new JsonArray(new JsonObject
                {
                    ["type"] = "pie",
                    ["radius"] = chart.Kind is VisualBriefingChartKind.DONUT ? Texts("45%", "70%") : Texts("0%", "70%"),
                    ["padAngle"] = 2,
                    ["itemStyle"] = new JsonObject { ["borderColor"] = "#FFFEFA", ["borderWidth"] = 2, ["borderRadius"] = 5 },
                    ["label"] = new JsonObject { ["color"] = "#4F635B" },
                    ["data"] = series,
                })
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

    private static JsonArray Texts(params IEnumerable<string> texts) => new([.. texts.Select(text => JsonValue.Create(text))]);

    private static JsonArray Numbers(IEnumerable<decimal> numbers) => new([.. numbers.Select(number => JsonValue.Create(number))]);
}