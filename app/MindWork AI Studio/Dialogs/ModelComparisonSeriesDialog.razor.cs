using AIStudio.Assistants.ModelComparison;
using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// The dialog with which the user begins a comparison series of the Model Comparison assistant. It stores the series
/// and closes with it.
/// </summary>
/// <remarks>
/// A fair comparison needs the same setup for both models. The selections therefore offer only the tools which both
/// models may use, and only the data sources which both models and the judge may see: the judge reads both chats, and
/// with them what the data sources gave. Both chats also search the data sources the same way, by Semantic Search only
/// when both models can use it, see SharedSelectionRules. What the series stores is checked once more the same way,
/// since a selection may still hold what a model changed since keeps from it.
/// </remarks>
public partial class ModelComparisonSeriesDialog : MSGComponentBase
{
    private const Tools.Components COMPONENT = Tools.Components.MODEL_COMPARISON_ASSISTANT;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    [Inject]
    private DataSourceService DataSourceService { get; init; } = null!;

    [Inject]
    private ModelComparisonStore ModelComparisonStore { get; init; } = null!;

    [Inject]
    private ILogger<ModelComparisonSeriesDialog> Logger { get; init; } = null!;

    private string seriesName = string.Empty;
    private AIStudio.Settings.Provider firstProvider = AIStudio.Settings.Provider.NONE;
    private AIStudio.Settings.Provider secondProvider = AIStudio.Settings.Provider.NONE;
    private bool judgeEnabled;
    private bool judgePanelExpanded;
    private AIStudio.Settings.Provider judgeProvider = AIStudio.Settings.Provider.NONE;
    private Profile profile = Profile.NO_PROFILE;
    private HashSet<string> selectedToolIds = [];

    /// <summary>
    /// The data source options both chats get. An AI never selects or validates the data sources of a series, since it
    /// could treat the two models differently, see ModelComparisonSeriesThreads.
    /// </summary>
    private DataSourceOptions dataSourceOptions = new()
    {
        DisableDataSources = true,
        AutomaticDataSourceSelection = false,
        AutomaticValidation = false,
        RetrievalMode = DataSourceRetrievalMode.SEMANTIC_SEARCH,
    };

    private string? issue;
    private bool isSaving;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.profile = this.SettingsManager.GetPreselectedProfile(COMPONENT);
        this.firstProvider = this.SettingsManager.PreselectedComparisonModel;
        this.judgeProvider = this.SettingsManager.PreselectedComparisonJudge;
        this.judgeEnabled = this.judgeProvider != AIStudio.Settings.Provider.NONE;
        this.judgePanelExpanded = this.judgeEnabled;
    }

    #endregion

    private bool BothModelsChosen => this.firstProvider.UsedLLMProvider is not LLMProviders.NONE && this.secondProvider.UsedLLMProvider is not LLMProviders.NONE;

    /// <summary>
    /// The second model, as the selections take further models.
    /// </summary>
    private IReadOnlyList<AIStudio.Settings.Provider> SecondModel => [this.secondProvider];

    /// <summary>
    /// The models which read what the data sources gave without searching them: the judge, while it is switched on.
    /// </summary>
    private IReadOnlyList<AIStudio.Settings.Provider> Readers => this.judgeEnabled && this.judgeProvider.UsedLLMProvider is not LLMProviders.NONE
        ? [this.judgeProvider]
        : [];

    /// <summary>
    /// Says in short what is selected, next to the buttons which hide the selections in their popovers.
    /// </summary>
    private string SelectionSummary => string.Format(
        T("Tools: {0}, data sources: {1}"),
        this.selectedToolIds.Count.CompactCount(),
        (this.dataSourceOptions.DisableDataSources ? 0 : this.dataSourceOptions.PreselectedDataSourceIds.Count).CompactCount());

    private void FirstProviderChanged(AIStudio.Settings.Provider provider) => this.firstProvider = provider;

    private void SecondProviderChanged(AIStudio.Settings.Provider provider) => this.secondProvider = provider;

    private void JudgeEnabledChanged(bool enabled) => this.judgeEnabled = enabled;

    private void JudgeProviderChanged(AIStudio.Settings.Provider provider) => this.judgeProvider = provider;

    private void ProfileChanged(Profile changedProfile) => this.profile = changedProfile;

    private void ToolsChanged(HashSet<string> toolIds) => this.selectedToolIds = toolIds;

    private void DataSourceOptionsChanged(DataSourceOptions options) => this.dataSourceOptions = options;

    /// <summary>
    /// Checks the form and tells the user what is missing.
    /// </summary>
    /// <returns>The reason, or null when the series can begin.</returns>
    private string? CheckForm()
    {
        if (string.IsNullOrWhiteSpace(this.seriesName))
            return TA("Please give the series a name, so that you find it again.");

        if (ModelComparisonProviderCheck.IssueOf(this.SettingsManager, this.firstProvider) is { } firstIssue)
            return firstIssue;

        if (ModelComparisonProviderCheck.IssueOf(this.SettingsManager, this.secondProvider) is { } secondIssue)
            return secondIssue;

        if (ModelComparisonModelIdentity.Of(this.firstProvider).IsSameModelAs(ModelComparisonModelIdentity.Of(this.secondProvider)))
            return TA("Please choose two different models. A series compares how well two models help you.");

        if (this.judgeEnabled && ModelComparisonProviderCheck.IssueOf(this.SettingsManager, this.judgeProvider) is { } judgeIssue)
        {
            this.judgePanelExpanded = true;
            return judgeIssue;
        }

        return null;
    }

    /// <summary>
    /// Gets the selected tools which both models may use.
    /// </summary>
    private List<string> ToolsForBoth()
    {
        var toolIds = this.ToolRegistry.NormalizeSelection(this.selectedToolIds)
            .Where(toolId => this.MayUseTool(this.firstProvider, toolId) && this.MayUseTool(this.secondProvider, toolId));

        return [.. this.ToolRegistry.NormalizeSelection(toolIds)];
    }

    /// <summary>
    /// Whether a model may use a tool. Which data a chat holds is decided per request; here, nothing restricts it yet.
    /// </summary>
    private bool MayUseTool(AIStudio.Settings.Provider provider, string toolId) => this.ToolRegistry.FilterToolIdsForProvider(provider, [toolId], OutboundDataRestriction.UNRESTRICTED).Count > 0;

    /// <summary>
    /// Gets the selected data sources which every model that sees the data may see, and the way both chats search them.
    /// </summary>
    /// <returns>The IDs of the data sources, and how they are searched, or null without any data source.</returns>
    private async Task<(List<string> DataSourceIds, DataSourceRetrievalMode? RetrievalMode)> DataSourcesForAllAsync()
    {
        if (this.dataSourceOptions.DisableDataSources || !PreviewFeatures.PRE_RAG_2024.IsEnabled(this.SettingsManager))
            return ([], null);

        List<EffectiveRetrievalMode> modes = [];
        foreach (var provider in new[] { this.firstProvider, this.secondProvider })
            modes.Add(await this.ToolRegistry.GetEffectiveRetrievalModeAsync(this.dataSourceOptions, provider, COMPONENT));

        var retrievalMode = SharedSelectionRules.RetrievalModeOfAll(modes).Mode;
        var preselectedDataSources = this.SettingsManager.ConfigurationData.DataSources
            .Where(dataSource => this.dataSourceOptions.PreselectedDataSourceIds.Contains(dataSource.Id))
            .ToList();

        List<AllowedSelectedDataSources> results = [];
        foreach (var provider in new[] { this.firstProvider, this.secondProvider }.Concat(this.Readers))
            results.Add(await this.DataSourceService.GetDataSources(provider, this.dataSourceOptions, retrievalMode, preselectedDataSources));

        List<string> dataSourceIds = [.. SharedSelectionRules.DataSourcesOfAll(results).SelectedDataSources.Select(dataSource => dataSource.Id)];
        return (dataSourceIds, dataSourceIds.Count > 0 ? retrievalMode : null);
    }

    private async Task CreateAsync()
    {
        this.issue = this.CheckForm();
        if (this.issue is not null)
            return;

        this.isSaving = true;
        try
        {
            var (dataSourceIds, retrievalMode) = await this.DataSourcesForAllAsync();
            var series = new ModelComparisonSeries
            {
                Id = Guid.NewGuid(),
                Name = this.seriesName.Trim(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                FirstProviderId = this.firstProvider.Id,
                SecondProviderId = this.secondProvider.Id,
                First = ModelComparisonModelIdentity.Of(this.firstProvider),
                Second = ModelComparisonModelIdentity.Of(this.secondProvider),
                JudgeProviderId = this.judgeEnabled ? this.judgeProvider.Id : string.Empty,
                Judge = this.judgeEnabled ? ModelComparisonModelIdentity.Of(this.judgeProvider) : null,
                ProfileId = this.profile.Id,
                ToolIds = this.ToolsForBoth(),
                DataSourceIds = dataSourceIds,
                RetrievalMode = retrievalMode,
            };

            await this.ModelComparisonStore.SaveSeriesAsync(series);
            this.MudDialog.Close(DialogResult.Ok(series));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not store a new comparison series of the Model Comparison assistant.");
            await this.MessageBus.SendError(new(AppIcons.ASSISTANT_MODEL_COMPARISON, TA("AI Studio could not save the new comparison series. Please try again.")));
        }
        finally
        {
            this.isSaving = false;
        }
    }

    private void Cancel() => this.MudDialog.Cancel();
}