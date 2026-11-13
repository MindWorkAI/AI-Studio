using AIStudio.Chat.Embedded;
using AIStudio.Components;
using AIStudio.Dialogs;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The mode of the Model Comparison assistant in which the user works on real tasks with both models, one after the
/// other, and decides which chat helped more.
/// </summary>
/// <remarks>
/// The page shows one of three views: the list of the comparison series, the form of a new series, or an opened
/// series. A series lives in ModelComparisonStore, so the user can continue it after days or weeks.
/// </remarks>
public partial class AssistantModelComparisonTasks : MSGComponentBase
{
    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    [Inject]
    private ModelComparisonStore ModelComparisonStore { get; init; } = null!;

    [Inject]
    private EmbeddedChatService ChatService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<AssistantModelComparisonTasks> Logger { get; init; } = null!;

    /// <summary>
    /// All comparison series, or null while they load.
    /// </summary>
    private IReadOnlyList<ModelComparisonSeries>? allSeries;

    /// <summary>
    /// Whether the form of a new series shows.
    /// </summary>
    private bool showsSetup;

    /// <summary>
    /// The series the user opened, or null while the list shows.
    /// </summary>
    private ModelComparisonSeries? openSeries;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (!ModelComparisonAccess.CanOpen(this.SettingsManager, T("Model Comparison")))
        {
            this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
            return;
        }

        await this.ReloadSeriesAsync();
    }

    #endregion

    private async Task ReloadSeriesAsync() => this.allSeries = await this.ModelComparisonStore.ListSeriesAsync();

    /// <summary>
    /// Describes a series in the list without naming its models: their names would end the blindness.
    /// </summary>
    private string DescribeSeries(ModelComparisonSeries series)
    {
        var culture = Tools.PluginSystem.I18N.I.Culture;
        var decided = series.Tasks.Count(task => task.State is ModelComparisonSeriesTaskState.DONE).CompactCount();
        var started = CommonTools.FormatTimestampToGeneral(series.CreatedAtUtc.ToLocalTime().DateTime, culture);
        return series.IsResolved
            ? string.Format(culture, T("Started {0}, decided tasks: {1}, result shown"), started, decided)
            : string.Format(culture, T("Started {0}, decided tasks: {1}"), started, decided);
    }

    private void OpenSetup() => this.showsSetup = true;

    private void CloseSetup() => this.showsSetup = false;

    private async Task SeriesCreatedAsync(ModelComparisonSeries series)
    {
        this.showsSetup = false;
        await this.ReloadSeriesAsync();
        this.OpenSeries(series);
    }

    private void OpenSeries(ModelComparisonSeries series) => this.openSeries = series;

    private async Task CloseSeries()
    {
        this.openSeries = null;
        await this.ReloadSeriesAsync();
    }

    /// <summary>
    /// Deletes a series with its chats after asking.
    /// </summary>
    private async Task DeleteSeriesAsync(ModelComparisonSeries series)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, string.Format(TA("Are you sure you want to delete the comparison series '{0}' with all its chats? This cannot be undone."), series.Name) },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete series"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        // An answer which still runs would otherwise try to store its chat into the deleted series:
        foreach (var task in series.Tasks)
        {
            await this.ChatService.ForgetAsync(task.ChatOne.ChatId);
            await this.ChatService.ForgetAsync(task.ChatTwo.ChatId);
        }

        try
        {
            await this.ModelComparisonStore.DeleteSeriesAsync(series.Id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not delete a comparison series of the Model Comparison assistant.");
            await this.MessageBus.SendError(new(AppIcons.DELETE, TA("AI Studio could not delete the comparison series. Please try again.")));
        }

        await this.ReloadSeriesAsync();
    }

    /// <summary>
    /// Shows what all stored comparisons say about the models, the series whose result the user saw included.
    /// </summary>
    private async Task OpenResultsDialog() => await this.DialogService.ShowAsync<ModelComparisonResultsDialog>(T("Results of all comparisons"), DialogOptions.FULLSCREEN);

    /// <summary>
    /// Goes back to the choice between the two modes.
    /// </summary>
    private void SwitchMode() => this.NavigationManager.NavigateTo(Routes.ASSISTANT_MODEL_COMPARISON);
}