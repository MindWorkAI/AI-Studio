using AIStudio.Assistants.ModelComparison;
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

    /// <summary>
    /// Reads all stored comparisons anew.
    /// </summary>
    private async Task LoadStatisticsAsync()
    {
        this.statistics = null;
        this.statistics = await ModelComparisonStatistics.ComputeAsync(this.ModelComparisonStore.ReadResultsAsync());
    }

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