using AIStudio.Assistants.ModelComparison;
using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

using MudBlazor.Utilities;

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

    /// <summary>
    /// How long the name of a series may be, see ModelComparisonNames.
    /// </summary>
    private const int MAX_NAME_LENGTH = ModelComparisonNames.MAX_SERIES_NAME_LENGTH;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The series whose setup the new one takes over, or null for a new series with the preselected options.
    /// </summary>
    /// <remarks>
    /// Only the setup carries over; the tasks stay in their series. What is no longer available is left out, see
    /// ModelComparisonSeriesCopy.
    /// </remarks>
    [Parameter]
    public ModelComparisonSeries? BasedOn { get; set; }

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    [Inject]
    private DataSourceService DataSourceService { get; init; } = null!;

    [Inject]
    private ModelComparisonStore ModelComparisonStore { get; init; } = null!;

    [Inject]
    private ILogger<ModelComparisonSeriesDialog> Logger { get; init; } = null!;

    private string seriesName = string.Empty;

    /// <summary>
    /// The names of the existing series, which a new series must not take again.
    /// </summary>
    private IReadOnlyList<string> takenSeriesNames = [];

    private AIStudio.Settings.Provider firstProvider = AIStudio.Settings.Provider.NONE;
    private AIStudio.Settings.Provider secondProvider = AIStudio.Settings.Provider.NONE;
    private bool judgeEnabled;
    private bool judgePanelExpanded;
    private AIStudio.Settings.Provider judgeProvider = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// The expectations, values, and goals of the user, by which the judge decides, see ModelComparisonJudgeExpectations.
    /// </summary>
    private string judgeInstructions = string.Empty;

    /// <summary>
    /// Whether the user confirmed that the judge may decide by its own standards, which counts only while
    /// judgeInstructions is empty.
    /// </summary>
    private bool judgeOwnStandardsAccepted;

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

    /// <summary>
    /// The parts of the series in BasedOn which the new series cannot take over.
    /// </summary>
    private IReadOnlyList<ModelComparisonSeriesPart> missingParts = [];

    private bool dataIsValid;
    private string[] dataIssues = [];

    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    /// <summary>
    /// Whether the user has tried to create the series at least once. The field of the judge model mounts only once
    /// the judge is switched on, and it stays silent until then, see ValidatingJudgeModel. Afterward, every change
    /// checks the form anew, see RevalidateAfterFieldChange.
    /// </summary>
    private bool hasAttemptedCreate;

    /// <summary>
    /// Whether the form has to be checked anew once the fields hold their new values, i.e., after the next render.
    /// </summary>
    private bool revalidatesAfterRender;

    private bool isSaving;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.takenSeriesNames = [.. (await this.ModelComparisonStore.ListSeriesAsync()).Select(existingSeries => existingSeries.Name)];
        if (this.BasedOn is { } series)
        {
            var selectableToolIds = await this.ToolRegistry.FilterSelectableToolIdsAsync(COMPONENT, series.ToolIds);
            var copy = ModelComparisonSeriesCopy.Of(this.SettingsManager, series, selectableToolIds);

            // The name of the series itself is taken, so the new one counts on, e.g., "Weekly reports (2)":
            this.seriesName = ModelComparisonNames.FreeNameFrom(series.Name, this.takenSeriesNames, MAX_NAME_LENGTH);
            this.firstProvider = copy.First;
            this.secondProvider = copy.Second;
            this.judgeEnabled = copy.JudgeEnabled;
            this.judgePanelExpanded = copy.JudgeEnabled;
            this.judgeProvider = copy.Judge;
            this.judgeInstructions = series.JudgeInstructions;
            this.profile = copy.Profile;
            this.selectedToolIds = copy.ToolIds;
            this.dataSourceOptions = copy.DataSourceOptions;
            this.missingParts = copy.MissingParts;
            return;
        }

        this.profile = this.SettingsManager.GetPreselectedProfile(COMPONENT);
        this.firstProvider = this.SettingsManager.PreselectedComparisonModel;
        this.judgeProvider = this.SettingsManager.PreselectedComparisonJudge;
        this.judgeEnabled = this.judgeProvider != AIStudio.Settings.Provider.NONE;
        this.judgePanelExpanded = this.judgeEnabled;
    }

    #endregion

    #region Overrides of ComponentBase

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // We don't want to show validation errors when the user opens the dialog:
        if (firstRender)
            await this.form.ResetValidationAsync();

        // A check asked for after a change waits until the fields hold their new values, cf. DependentFieldValidation:
        if (this.revalidatesAfterRender)
        {
            this.revalidatesAfterRender = false;
            await this.form.ValidateAsync();
        }

        await base.OnAfterRenderAsync(firstRender);
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

    /// <summary>
    /// Tells what the new series starts with, when it takes over the setup of another one.
    /// </summary>
    private string BasedOnText => string.Format(TA("This series starts with the setup of the series '{0}'. You can change all of it before you create the series. The tasks stay in their series."), this.BasedOn?.Name);

    /// <summary>
    /// Tells why a part of the series in BasedOn is left out, and what the user can do about it.
    /// </summary>
    private string MissingPartText(ModelComparisonSeriesPart part) => part switch
    {
        ModelComparisonSeriesPart.FIRST_MODEL => TA("The first model is no longer available, uses another model now, or may no longer be used by this assistant. Please choose it anew."),
        ModelComparisonSeriesPart.SECOND_MODEL => TA("The second model is no longer available, uses another model now, or may no longer be used by this assistant. Please choose it anew."),
        ModelComparisonSeriesPart.JUDGE => TA("The judge model is no longer available, uses another model now, or may no longer be used by this assistant. Please choose another one, or switch the judge off."),
        ModelComparisonSeriesPart.PROFILE => TA("The profile no longer exists, so the series starts without a profile."),
        ModelComparisonSeriesPart.TOOLS => TA("Some tools are no longer available, so the series starts without them."),
        ModelComparisonSeriesPart.DATA_SOURCES => TA("Some data sources are no longer available, so the series starts without them."),
        _ => string.Empty,
    };

    /// <summary>
    /// Checks the name of the series, see ModelComparisonNames.SeriesNameIssue.
    /// </summary>
    /// <returns>The issue, or null when the name is fine.</returns>
    private string? ValidatingSeriesName(string name) => ModelComparisonNames.SeriesNameIssue(name, this.takenSeriesNames);

    private string? ValidatingFirstModel(AIStudio.Settings.Provider provider) => ModelComparisonProviderCheck.IssueOf(this.SettingsManager, provider);

    /// <summary>
    /// Checks the second model, which must be another model than the first one: a series compares how well two models
    /// help the user.
    /// </summary>
    /// <returns>The issue, or null when the model is fine.</returns>
    private string? ValidatingSecondModel(AIStudio.Settings.Provider provider)
    {
        if (ModelComparisonProviderCheck.IssueOf(this.SettingsManager, provider) is { } issue)
            return issue;

        return ModelComparisonModelIdentity.Of(this.firstProvider).IsSameModelAs(ModelComparisonModelIdentity.Of(provider))
            ? TA("Please choose two different models. A series compares how well two models help you.")
            : null;
    }

    /// <summary>
    /// Checks the judge model.
    /// </summary>
    /// <remarks>
    /// Silent until hasAttemptedCreate: this field mounts only once the judge is switched on, often well after the form
    /// first rendered, and MudForm would otherwise flag it as soon as it exists, before the user had any chance to fill
    /// it in. The same holds for the judge model of the mode Answer Variance.
    /// </remarks>
    private string? ValidatingJudgeModel(AIStudio.Settings.Provider provider) => this.hasAttemptedCreate ? ModelComparisonProviderCheck.IssueOf(this.SettingsManager, provider) : null;

    /// <summary>
    /// Whether one of the fields of the switched-on judge has an issue: its model, or the expectations of the user
    /// without the confirmation that the judge may decide by its own standards.
    /// </summary>
    private bool JudgeHasIssue => this.judgeEnabled && (ModelComparisonProviderCheck.IssueOf(this.SettingsManager, this.judgeProvider) is not null || ModelComparisonJudgeExpectations.IssueOf(this.judgeInstructions, this.judgeOwnStandardsAccepted) is not null);

    /// <remarks>
    /// The second model is checked against the first one, which a MudForm does not know: it checks a field only when
    /// that very field changes. Once the user tried to create the series and saw the issues, every change checks the
    /// whole form anew, so that an issue which no longer holds goes away.
    /// </remarks>
    private void RevalidateAfterFieldChange(FormFieldChangedEventArgs _)
    {
        if (this.hasAttemptedCreate)
            this.revalidatesAfterRender = true;
    }

    private void FirstProviderChanged(AIStudio.Settings.Provider provider) => this.firstProvider = provider;

    private void SecondProviderChanged(AIStudio.Settings.Provider provider) => this.secondProvider = provider;

    private void JudgeEnabledChanged(bool enabled) => this.judgeEnabled = enabled;

    private void JudgeProviderChanged(AIStudio.Settings.Provider provider) => this.judgeProvider = provider;

    private void ProfileChanged(Profile changedProfile) => this.profile = changedProfile;

    private void ToolsChanged(HashSet<string> toolIds) => this.selectedToolIds = toolIds;

    private void DataSourceOptionsChanged(DataSourceOptions options) => this.dataSourceOptions = options;

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
    /// <returns>The data sources and how they are searched, or NONE without any data source.</returns>
    private async Task<ModelComparisonSeriesDataSources> DataSourcesForAllAsync()
    {
        if (this.dataSourceOptions.DisableDataSources || !PreviewFeatures.PRE_RAG_2024.IsEnabled(this.SettingsManager))
            return ModelComparisonSeriesDataSources.NONE;

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
        return dataSourceIds.Count > 0
            ? new(dataSourceIds, retrievalMode)
            : ModelComparisonSeriesDataSources.NONE;
    }

    private async Task CreateAsync()
    {
        // Set before validating, so the judge model -- silent about being empty until now -- reports its issue at once:
        this.hasAttemptedCreate = true;
        await this.form.ValidateAsync();

        //
        // The fields of the judge sit in a panel the user may have closed. The form checks them all the same, but the
        // user would not see which one is wrong. So the dialog opens the panel:
        //
        if (this.JudgeHasIssue)
            this.judgePanelExpanded = true;

        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;

        this.isSaving = true;
        try
        {
            var dataSources = await this.DataSourcesForAllAsync();
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
                JudgeInstructions = this.judgeEnabled ? this.judgeInstructions.Trim() : string.Empty,
                ProfileId = this.profile.Id,
                ToolIds = this.ToolsForBoth(),
                DataSourceIds = [.. dataSources.DataSourceIds],
                RetrievalMode = dataSources.RetrievalMode,
            };

            await this.ModelComparisonStore.CreateSeriesAsync(series);
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