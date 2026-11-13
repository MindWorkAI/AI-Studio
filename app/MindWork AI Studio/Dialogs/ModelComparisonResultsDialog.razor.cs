using System.Globalization;

using AIStudio.Assistants.ModelComparison;
using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Shows what all stored comparisons of the Model Comparison assistant say about the models: which model wins, which
/// wins against which, how often the judges agree with the user, and how fast each model answers.
/// </summary>
/// <remarks>
/// The colors of the charts are fixed rather than taken from the theme, because MudBlazor paints the bars with the
/// colors of the palette as they are. Blue for wins, gray for ties, and red for losses read on the light and on the
/// dark theme alike, and stay apart for readers with a color vision deficiency; the axes and the legend follow the
/// theme on their own. On the dark theme, blue and red stay slightly below a contrast of 3:1, so every chart comes with
/// a table of the same numbers, which reads without any color.
/// </remarks>
public partial class ModelComparisonResultsDialog : MSGComponentBase
{
    /// <summary>
    /// The colors of wins, ties, and losses, in the order of the series.
    /// </summary>
    private static readonly string[] OUTCOME_PALETTE = ["#2a78d6", "#898781", "#e34948"];

    /// <summary>
    /// The colors of the time until the first token and until the complete answer.
    /// </summary>
    private static readonly string[] SPEED_PALETTE = ["#2a78d6", "#d95926"];

    /// <summary>
    /// Above this many models, the names below the bars are tilted, so that long names do not run into each other.
    /// </summary>
    private const int MAX_UPRIGHT_LABELS = 4;

    /// <summary>
    /// About how many steps the vertical axis of a chart shows.
    /// </summary>
    private const int AXIS_STEP_COUNT = 5;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private ModelComparisonStore ModelComparisonStore { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<ModelComparisonResultsDialog> Logger { get; init; } = null!;

    /// <summary>
    /// The statistics of all stored comparisons, or null while they are being read.
    /// </summary>
    private ModelComparisonStatistics? statistics;

    private bool isDeleting;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await this.LoadStatisticsAsync();
    }

    #endregion

    private static CultureInfo DisplayCulture => I18N.I.Culture;

    /// <summary>
    /// Reads all stored comparisons anew.
    /// </summary>
    private async Task LoadStatisticsAsync()
    {
        this.statistics = null;
        this.statistics = await ModelComparisonStatistics.ComputeAsync(this.ModelComparisonStore.ReadResultsAsync());
    }

    /// <summary>
    /// The names of the models below the bars of a chart.
    /// </summary>
    private static string[] ModelLabels(IEnumerable<ModelComparisonModelIdentity> models) => [.. models.Select(model => model.Label)];

    /// <summary>
    /// The wins, ties, and losses of each model, as the three series of a stacked bar chart.
    /// </summary>
    private List<ChartSeries<double>> OutcomeSeries(IReadOnlyList<ModelComparisonModelStanding> ranking) =>
    [
        new() { Name = T("Wins"), Data = new([.. ranking.Select(standing => (double)standing.Wins)]) },
        new() { Name = T("Ties"), Data = new([.. ranking.Select(standing => (double)standing.Ties)]) },
        new() { Name = T("Losses"), Data = new([.. ranking.Select(standing => (double)standing.Losses)]) },
    ];

    private static StackedBarChartOptions OutcomeChartOptions(IReadOnlyList<ModelComparisonModelStanding> ranking) => new()
    {
        ChartPalette = OUTCOME_PALETTE,
        YAxisTicks = AxisStep(ranking.Select(standing => (double)(standing.Wins + standing.Ties + standing.Losses))),
        YAxisToStringFunc = value => ((long)value).CompactCount(),
        XAxisLabelRotation = ranking.Count > MAX_UPRIGHT_LABELS ? 30 : 0,
    };

    /// <summary>
    /// The models with complete answers, whose median times a chart can show.
    /// </summary>
    private static List<ModelComparisonModelSpeed> TimedSpeeds(IEnumerable<ModelComparisonModelSpeed> speeds) => [.. speeds.Where(speed => speed is { MedianFirstTokenTime: not null, MedianTotalTime: not null })];

    /// <summary>
    /// The median times of each model in seconds, as the two series of a bar chart.
    /// </summary>
    private List<ChartSeries<double>> SpeedSeries(IReadOnlyList<ModelComparisonModelSpeed> speeds) =>
    [
        new() { Name = T("Until the first token"), Data = new([.. speeds.Select(speed => speed.MedianFirstTokenTime!.Value.TotalSeconds)]) },
        new() { Name = T("Until the complete answer"), Data = new([.. speeds.Select(speed => speed.MedianTotalTime!.Value.TotalSeconds)]) },
    ];

    private static BarChartOptions SpeedChartOptions(IReadOnlyList<ModelComparisonModelSpeed> speeds) => new()
    {
        ChartPalette = SPEED_PALETTE,
        YAxisTicks = AxisStep(speeds.Select(speed => speed.MedianTotalTime!.Value.TotalSeconds)),
        YAxisToStringFunc = FormatSeconds,
        XAxisLabelRotation = speeds.Count > MAX_UPRIGHT_LABELS ? 30 : 0,
    };

    /// <summary>
    /// Gets a step for the vertical axis: a whole 1, 2, or 5 times a power of ten, so that the axis shows about
    /// AXIS_STEP_COUNT steps up to the largest value. Counts have no fractions, and seconds need none on an axis.
    /// </summary>
    /// <param name="values">The values of the chart.</param>
    /// <returns>The step.</returns>
    private static int AxisStep(IEnumerable<double> values)
    {
        var maximum = values.DefaultIfEmpty(0).Max();
        for (var magnitude = 1; ; magnitude *= 10)
            foreach (var factor in (int[])[1, 2, 5])
                if (maximum / (factor * magnitude) <= AXIS_STEP_COUNT)
                    return factor * magnitude;
    }

    private static string FormatShare(double? share) => share is { } value
        ? value.ToString("P0", DisplayCulture)
        : "-";

    private string FormatRange(ModelComparisonWinShare winShare) => winShare is { LowerBound: { } lower, UpperBound: { } upper }
        ? string.Format(DisplayCulture, T("{0} to {1}"), FormatShare(lower), FormatShare(upper))
        : "-";

    private static string FormatSeconds(double seconds) => $"{seconds.ToString("0.0", DisplayCulture)} s";

    private static string FormatDuration(TimeSpan? duration) => duration is { } value
        ? FormatSeconds(value.TotalSeconds)
        : "-";

    private string SignificanceText(ModelComparisonSignificance significance) => significance switch
    {
        ModelComparisonSignificance.CLEARLY_BETTER => T("Clearly better"),
        ModelComparisonSignificance.CLEARLY_WORSE => T("Clearly worse"),
        _ => T("Not yet clear"),
    };

    /// <summary>
    /// Says which model of a pair won clearly, if one did.
    /// </summary>
    /// <param name="pairing">The pair.</param>
    /// <param name="winShareOfFirst">The share of wins of the first model, by the votes or by the judges.</param>
    private string PairSignificanceText(ModelComparisonPairing pairing, ModelComparisonWinShare winShareOfFirst) => winShareOfFirst.Significance switch
    {
        ModelComparisonSignificance.CLEARLY_BETTER => string.Format(T("{0} is clearly better"), pairing.First.Label),
        ModelComparisonSignificance.CLEARLY_WORSE => string.Format(T("{0} is clearly better"), pairing.Second.Label),
        _ => T("Not yet clear"),
    };

    /// <summary>
    /// The icon of an assessment. A pair which one model won clearly gets the icon of the better one.
    /// </summary>
    private static string SignificanceIcon(ModelComparisonSignificance significance, bool isPair) => significance switch
    {
        ModelComparisonSignificance.CLEARLY_BETTER => AppIcons.CLEARLY_BETTER,
        ModelComparisonSignificance.CLEARLY_WORSE => isPair ? AppIcons.CLEARLY_BETTER : AppIcons.CLEARLY_WORSE,
        _ => AppIcons.NOT_YET_CLEAR,
    };

    private static Color SignificanceColor(ModelComparisonSignificance significance, bool isPair) => significance switch
    {
        ModelComparisonSignificance.CLEARLY_BETTER => Color.Success,
        ModelComparisonSignificance.CLEARLY_WORSE => isPair ? Color.Success : Color.Error,
        _ => Color.Default,
    };

    private async Task DeleteAllResultsAsync()
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, TA("Are you sure you want to delete the results of all your comparisons? Your presets stay. This cannot be undone.") },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete all results"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        this.isDeleting = true;
        try
        {
            await this.ModelComparisonStore.DeleteAllResultsAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not delete the results of the Model Comparison assistant.");
            await this.MessageBus.SendError(new(AppIcons.ASSISTANT_MODEL_COMPARISON, T("AI Studio could not delete all results. Please try again.")));
        }
        finally
        {
            this.isDeleting = false;
        }

        // Reads anew in any case: a failure may have deleted a part of the results already.
        await this.LoadStatisticsAsync();
    }

    private void Close() => this.MudDialog.Close(DialogResult.Ok(true));
}