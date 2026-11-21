using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Dialogs.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The start page of the Model Comparison assistant, where the user chooses between its two modes.
/// </summary>
/// <remarks>
/// The tile of the assistant leads here. Each mode has a page of its own, so that the assistant session of the
/// stability mode survives a visit to the other mode, and a link or the browser history may lead to either directly.
/// </remarks>
public partial class ModelComparisonModeChoice : MSGComponentBase
{
    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (!ModelComparisonAccess.CanOpen(this.SettingsManager, T("Model Comparison")))
            this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
    }

    #endregion

    private IReadOnlyList<AssistantModeOption<ModelComparisonMode>> Options =>
    [
        new(ModelComparisonMode.STABILITY, ModelComparisonMode.STABILITY.Icon, ModelComparisonMode.STABILITY.Name, TA("Send the same request to both models, once or many times in a row, and vote blindly for the better answer each time. You learn which model answers better, and how much its answers vary from run to run. An optional judge model gives its own verdict. At the end, you see which model wrote which answer.")),
        new(ModelComparisonMode.TASKS, ModelComparisonMode.TASKS.Icon, ModelComparisonMode.TASKS.Name, TA("Work on a real task of your everyday work with one of the two models, with follow-up questions, tools, and data sources, and then on the same task with the other one. Afterward, you decide which chat helped you more, without knowing which model was which. Each task adds to the result, over days or weeks.")),
    ];

    private void ChooseMode(ModelComparisonMode mode) => this.NavigationManager.NavigateTo(mode.Route);

    /// <summary>
    /// Shows what all stored comparisons say about the models, as both modes do.
    /// </summary>
    private async Task OpenResultsDialog() => await this.DialogService.ShowAsync<ModelComparisonResultsDialog>(T("Results of all comparisons"), DialogOptions.FULLSCREEN);

    /// <summary>
    /// Opens the options of the assistant, which both modes share.
    /// </summary>
    private async Task OpenSettingsDialog() => await this.DialogService.ShowAsync<SettingsDialogModelComparison>(null, new DialogParameters(), DialogOptions.FULLSCREEN);
}