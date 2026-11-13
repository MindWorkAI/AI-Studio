using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The form with which the user begins a comparison series of the Model Comparison assistant.
/// </summary>
/// <remarks>
/// A fair comparison needs the same setup for both models. The form therefore offers only the tools which both models
/// may use, and only the data sources which both models and the judge may see: the judge reads both chats, and with
/// them what the data sources gave. Both chats also search the data sources the same way, by Semantic Search only when
/// both models can use it, and otherwise with every message.
/// </remarks>
public partial class ModelComparisonSeriesSetup : MSGComponentBase
{
    private const Tools.Components COMPONENT = Tools.Components.MODEL_COMPARISON_ASSISTANT;

    /// <summary>
    /// Gets or sets what happens once the series is stored.
    /// </summary>
    [Parameter]
    public EventCallback<ModelComparisonSeries> SeriesCreated { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user does not want a new series after all.
    /// </summary>
    [Parameter]
    public EventCallback Canceled { get; set; }

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    [Inject]
    private DataSourceService DataSourceService { get; init; } = null!;

    [Inject]
    private ModelComparisonStore ModelComparisonStore { get; init; } = null!;

    [Inject]
    private ILogger<ModelComparisonSeriesSetup> Logger { get; init; } = null!;

    private string seriesName = string.Empty;
    private AIStudio.Settings.Provider firstProvider = AIStudio.Settings.Provider.NONE;
    private AIStudio.Settings.Provider secondProvider = AIStudio.Settings.Provider.NONE;
    private bool judgeEnabled;
    private AIStudio.Settings.Provider judgeProvider = AIStudio.Settings.Provider.NONE;
    private Profile profile = Profile.NO_PROFILE;
    private IReadOnlyList<ToolCatalogItem> offeredTools = [];
    private IReadOnlyCollection<string> selectedToolIds = [];
    private IReadOnlyList<IDataSource> offeredDataSources = [];
    private IReadOnlyCollection<string> selectedDataSourceIds = [];
    private DataSourceRetrievalMode retrievalMode = DataSourceRetrievalMode.SEMANTIC_SEARCH;
    private string? issue;
    private bool isSaving;

    private bool BothModelsChosen => this.firstProvider.UsedLLMProvider is not LLMProviders.NONE && this.secondProvider.UsedLLMProvider is not LLMProviders.NONE;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.profile = this.SettingsManager.GetPreselectedProfile(COMPONENT);
    }

    #endregion

    private string ToolsHelperText => this.offeredTools.Count == 0
        ? T("Both models together may use no tool.")
        : T("Only the tools which both models may use.");

    private string DataSourcesHelperText => this.offeredDataSources.Count == 0
        ? T("Both models together may see no data source.")
        : this.retrievalMode is DataSourceRetrievalMode.SEMANTIC_SEARCH
            ? T("Both models search the data sources themselves, whenever a question calls for it.")
            : T("AI Studio searches the data sources with every message, since not both models can search them themselves.");

    private string? ToolName(string? toolId) => this.offeredTools.FirstOrDefault(tool => tool.Id == toolId)?.DisplayName ?? toolId;

    private string? DataSourceName(string? dataSourceId) => this.offeredDataSources.FirstOrDefault(dataSource => dataSource.Id == dataSourceId)?.Name ?? dataSourceId;

    private async Task FirstProviderChanged(AIStudio.Settings.Provider provider)
    {
        this.firstProvider = provider;
        await this.RefreshOffersAsync();
    }

    private async Task SecondProviderChanged(AIStudio.Settings.Provider provider)
    {
        this.secondProvider = provider;
        await this.RefreshOffersAsync();
    }

    private async Task JudgeEnabledChanged(bool enabled)
    {
        this.judgeEnabled = enabled;
        await this.RefreshOffersAsync();
    }

    private async Task JudgeProviderChanged(AIStudio.Settings.Provider provider)
    {
        this.judgeProvider = provider;
        await this.RefreshOffersAsync();
    }

    private void ProfileChanged(Profile changedProfile) => this.profile = changedProfile;

    private void ToolsChanged(IReadOnlyCollection<string?>? toolIds) => this.selectedToolIds = [.. (toolIds ?? []).OfType<string>()];

    private void DataSourcesChanged(IReadOnlyCollection<string?>? dataSourceIds) => this.selectedDataSourceIds = [.. (dataSourceIds ?? []).OfType<string>()];

    /// <summary>
    /// Decides anew what both models may use, and drops what they no longer may from the selection.
    /// </summary>
    private async Task RefreshOffersAsync()
    {
        if (!this.BothModelsChosen)
        {
            this.offeredTools = [];
            this.offeredDataSources = [];
            this.selectedToolIds = [];
            this.selectedDataSourceIds = [];
            return;
        }

        var catalog = await this.ToolRegistry.GetCatalogAsync(COMPONENT);
        this.offeredTools = [.. catalog.Where(tool => tool is { IsActive: true, ConfigurationState.IsConfigured: true } && this.MayUseTool(this.firstProvider, tool.Id) && this.MayUseTool(this.secondProvider, tool.Id))];
        this.selectedToolIds = [.. this.selectedToolIds.Where(toolId => this.offeredTools.Any(tool => tool.Id == toolId))];

        // Semantic Search only when both models can use it, so that neither gets passages the other has to look for:
        var options = new DataSourceOptions { DisableDataSources = false, RetrievalMode = DataSourceRetrievalMode.SEMANTIC_SEARCH };
        var bothSearchThemselves = true;
        foreach (var provider in this.ComparedProviders)
            bothSearchThemselves &= (await this.ToolRegistry.GetEffectiveRetrievalModeAsync(options, provider, COMPONENT)).Mode is DataSourceRetrievalMode.SEMANTIC_SEARCH;

        this.retrievalMode = bothSearchThemselves ? DataSourceRetrievalMode.SEMANTIC_SEARCH : DataSourceRetrievalMode.EVERY_MESSAGE;
        options.RetrievalMode = this.retrievalMode;

        this.offeredDataSources = await this.DataSourcesForAllAsync(options);
        this.selectedDataSourceIds = [.. this.selectedDataSourceIds.Where(dataSourceId => this.offeredDataSources.Any(dataSource => dataSource.Id == dataSourceId))];
    }

    /// <summary>
    /// Gets the data sources which every model that sees the data may see.
    /// </summary>
    /// <param name="options">The data source options, with the way both chats search them.</param>
    /// <returns>The data sources, none while the preview of data sources is switched off.</returns>
    private async Task<IReadOnlyList<IDataSource>> DataSourcesForAllAsync(DataSourceOptions options)
    {
        if (!PreviewFeatures.PRE_RAG_2024.IsEnabled(this.SettingsManager))
            return [];

        List<IDataSource>? shared = null;
        foreach (var provider in this.ProvidersWhichSeeTheData)
        {
            var allowed = (await this.DataSourceService.GetDataSources(provider, options, this.retrievalMode)).AllowedDataSources;
            shared = shared is null ? [.. allowed] : [.. shared.Where(dataSource => allowed.Any(other => other.Id == dataSource.Id))];
        }

        return shared ?? [];
    }

    /// <summary>
    /// The two compared models.
    /// </summary>
    private IEnumerable<AIStudio.Settings.Provider> ComparedProviders => [this.firstProvider, this.secondProvider];

    /// <summary>
    /// The models which see what the data sources give: both compared models, and the judge, which reads both chats.
    /// </summary>
    private IEnumerable<AIStudio.Settings.Provider> ProvidersWhichSeeTheData => this.judgeEnabled && this.judgeProvider.UsedLLMProvider is not LLMProviders.NONE
        ? [.. this.ComparedProviders, this.judgeProvider]
        : this.ComparedProviders;

    /// <summary>
    /// Whether a model may use a tool. Which data a chat holds is decided per request; here, nothing restricts it yet.
    /// </summary>
    private bool MayUseTool(AIStudio.Settings.Provider provider, string toolId) => this.ToolRegistry.FilterToolIdsForProvider(provider, [toolId], OutboundDataRestriction.UNRESTRICTED).Count > 0;

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
            return judgeIssue;

        return null;
    }

    private async Task CreateAsync()
    {
        this.issue = this.CheckForm();
        if (this.issue is not null)
            return;

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
            ToolIds = [.. this.ToolRegistry.NormalizeSelection(this.selectedToolIds)],
            DataSourceIds = [.. this.selectedDataSourceIds],
            RetrievalMode = this.selectedDataSourceIds.Count > 0 ? this.retrievalMode : null,
        };

        this.isSaving = true;
        try
        {
            await this.ModelComparisonStore.SaveSeriesAsync(series);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not store a new comparison series of the Model Comparison assistant.");
            await this.MessageBus.SendError(new(AppIcons.ASSISTANT_MODEL_COMPARISON, TA("AI Studio could not save the new comparison series. Please try again.")));
            return;
        }
        finally
        {
            this.isSaving = false;
        }

        await this.SeriesCreated.InvokeAsync(series);
    }

    private Task CancelAsync() => this.Canceled.InvokeAsync();
}