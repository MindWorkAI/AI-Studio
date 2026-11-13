namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the charts of the results of the Model Comparison assistant have in common.
/// </summary>
/// <remarks>
/// The colors are fixed rather than taken from the theme, because MudBlazor paints the bars with the colors of the
/// palette as they are. Blue for wins, gray for ties, and red for losses read on the light and on the dark theme alike,
/// and stay apart for readers with a color vision deficiency; the axes and the legend follow the theme on their own. On
/// the dark theme, blue and red stay slightly below a contrast of 3:1, so every chart comes with a table of the same
/// numbers, which reads without any color.
/// </remarks>
public static class ModelComparisonCharts
{
    /// <summary>
    /// The colors of wins, ties, and losses, in the order of the series.
    /// </summary>
    public static readonly string[] OUTCOME_PALETTE = ["#2a78d6", "#898781", "#e34948"];

    /// <summary>
    /// The colors of the time until the first token and until the complete answer.
    /// </summary>
    public static readonly string[] SPEED_PALETTE = ["#2a78d6", "#d95926"];

    /// <summary>
    /// Above this many models, the names below the bars are tilted, so that long names do not run into each other.
    /// </summary>
    private const int MAX_UPRIGHT_LABELS = 4;

    /// <summary>
    /// About how many steps the vertical axis of a chart shows.
    /// </summary>
    private const int AXIS_STEP_COUNT = 5;

    /// <summary>
    /// Gets the names of the models below the bars of a chart.
    /// </summary>
    /// <param name="models">The models, in the order of the bars.</param>
    /// <returns>The names.</returns>
    public static string[] ModelLabels(IEnumerable<ModelComparisonModelIdentity> models) => [.. models.Select(model => model.Label)];

    /// <summary>
    /// Gets by how many degrees the names below the bars are tilted.
    /// </summary>
    /// <param name="modelCount">How many models the chart shows.</param>
    /// <returns>The angle, 0 for upright names.</returns>
    public static int LabelRotation(int modelCount) => modelCount > MAX_UPRIGHT_LABELS ? 30 : 0;

    /// <summary>
    /// Gets a step for the vertical axis: a whole 1, 2, or 5 times a power of ten, so that the axis shows about
    /// AXIS_STEP_COUNT steps up to the largest value. Counts have no fractions, and seconds need none on an axis.
    /// </summary>
    /// <param name="values">The values of the chart.</param>
    /// <returns>The step.</returns>
    public static int AxisStep(IEnumerable<double> values)
    {
        var maximum = values.DefaultIfEmpty(0).Max();
        for (var magnitude = 1; ; magnitude *= 10)
            foreach (var factor in (int[])[1, 2, 5])
                if (maximum / (factor * magnitude) <= AXIS_STEP_COUNT)
                    return factor * magnitude;
    }
}