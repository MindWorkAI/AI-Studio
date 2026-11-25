using AIStudio.Assistants.ModelComparison;
using AIStudio.Chat.Embedded;
using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Shows what all stored comparisons of the Model Comparison assistant say about the models: which model wins, which
/// wins against which, how often the judges agree with the user, and how fast each model answers.
/// </summary>
/// <remarks>
/// The charts and their tables are components of the assistant, see ModelComparisonRanking, ModelComparisonPairCard,
/// and ModelComparisonSpeeds; ModelComparisonCharts explains their colors.
/// </remarks>
public partial class ModelComparisonResultsDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private ModelComparisonStore ModelComparisonStore { get; init; } = null!;

    [Inject]
    private EmbeddedChatService ChatService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<ModelComparisonResultsDialog> Logger { get; init; } = null!;

    /// <summary>
    /// The statistics of all stored comparisons, or null while they are being read.
    /// </summary>
    private ModelComparisonStatistics? statistics;

    /// <summary>
    /// All comparison series, also those whose result the user has not seen yet; none while the source counts the
    /// compared answers alone.
    /// </summary>
    private IReadOnlyList<ModelComparisonSeries> listedSeries = [];

    /// <summary>
    /// Which results the statistics count.
    /// </summary>
    private ModelComparisonResultSource source = ModelComparisonResultSource.ALL;

    private bool isDeleting;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await this.LoadStatisticsAsync();
    }

    #endregion

    /// <summary>
    /// Reads the results the source names anew. The comparison series are read for every source but the compared
    /// answers alone: they are small, since their chats stay on the disk.
    /// </summary>
    private async Task LoadStatisticsAsync()
    {
        this.statistics = null;
        this.listedSeries = this.source is ModelComparisonResultSource.ANSWERS ? [] : await this.ModelComparisonStore.ListSeriesAsync();
        this.statistics = await ModelComparisonStatistics.ComputeAsync(this.ModelComparisonStore.ReadResultsAsync(), this.listedSeries, this.source);
    }

    private async Task SourceChanged(ModelComparisonResultSource changedSource)
    {
        this.source = changedSource;
        await this.LoadStatisticsAsync();
    }

    /// <summary>
    /// Whether deleting takes the results of the mode Answer Variance along: the chosen source counts them, and there
    /// are some.
    /// </summary>
    private bool DeletesResults => this.source is not ModelComparisonResultSource.TASKS && this.statistics is { ComparisonCount: > 0 };

    /// <summary>
    /// Whether deleting takes the comparison series along: the chosen source counts them, and there are some. The
    /// results of the mode Everyday Tasks are the decided tasks of the series, so deleting them means deleting the
    /// series, also those whose result the user has not seen yet, for a fresh start.
    /// </summary>
    private bool DeletesSeries => this.source is not ModelComparisonResultSource.ANSWERS && this.listedSeries.Count > 0;

    private string DeleteText => this.DeletesResults
        ? this.DeletesSeries ? T("Delete all results and comparison series") : T("Delete the results of “Answer Variance”")
        : T("Delete all comparison series");

    private string DeleteQuestion => this.DeletesResults
        ? this.DeletesSeries
            ? TA("Are you sure you want to delete all results of the mode “Answer Variance” and all comparison series of the mode “Everyday Tasks” with their chats, also those whose result you have not seen yet? Your presets stay. This cannot be undone.")
            : TA("Are you sure you want to delete all results of the mode “Answer Variance”? Your presets and the comparison series of the mode “Everyday Tasks” stay. This cannot be undone.")
        : TA("Are you sure you want to delete all comparison series of the mode “Everyday Tasks” with their chats, also those whose result you have not seen yet? Your presets and the results of the mode “Answer Variance” stay. This cannot be undone.");

    /// <summary>
    /// Deletes what the chosen source counts, after asking.
    /// </summary>
    private async Task DeleteAsync()
    {
        var deletesResults = this.DeletesResults;
        var deletesSeries = this.DeletesSeries;
        if (!deletesResults && !deletesSeries)
            return;

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, this.DeleteQuestion },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(this.DeleteText, parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        this.isDeleting = true;
        try
        {
            if (deletesResults)
                await this.DeleteResultsAsync();

            if (deletesSeries)
                await this.DeleteSeriesAsync();
        }
        finally
        {
            this.isDeleting = false;
        }

        // Reads anew in any case: a failure may have deleted a part already.
        await this.LoadStatisticsAsync();
    }

    private async Task DeleteResultsAsync()
    {
        try
        {
            await this.ModelComparisonStore.DeleteAllResultsAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not delete the results of the Model Comparison assistant.");
            await this.MessageBus.SendError(new(AppIcons.ASSISTANT_MODEL_COMPARISON, TA("AI Studio could not delete all results. Please try again.")));
        }
    }

    private async Task DeleteSeriesAsync()
    {
        // An answer which still runs would otherwise try to store its chat into a deleted series:
        foreach (var task in this.listedSeries.SelectMany(series => series.Tasks))
        {
            await this.ChatService.ForgetAsync(task.ChatOne.ChatId);
            await this.ChatService.ForgetAsync(task.ChatTwo.ChatId);
        }

        try
        {
            await this.ModelComparisonStore.DeleteAllSeriesAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not delete the comparison series of the Model Comparison assistant.");
            await this.MessageBus.SendError(new(AppIcons.ASSISTANT_MODEL_COMPARISON, TA("AI Studio could not delete all comparison series. Please try again.")));
        }
    }

    private void Close() => this.MudDialog.Close(DialogResult.Ok(true));
}