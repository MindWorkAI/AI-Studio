using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The setup which a new comparison series takes over from an existing one.
/// </summary>
/// <remarks>
/// A series keeps its setup as it was when it began. Meanwhile, a provider may have been deleted, use another model,
/// or no longer meet the requirements of the assistant, and a profile, a tool, or a data source may be gone. The new
/// series leaves such parts out, and MissingParts tells the user which. Whether both models may use what is left is
/// decided by the selections and by the dialog when it stores the series, as for every new series.
/// </remarks>
/// <param name="First">The first model, or NONE when the user has to choose it anew.</param>
/// <param name="Second">The second model, or NONE when the user has to choose it anew.</param>
/// <param name="JudgeEnabled">Whether the series asks a judge.</param>
/// <param name="Judge">The judge, or NONE without a judge or when the user has to choose it anew.</param>
/// <param name="Profile">The profile both chats use.</param>
/// <param name="ToolIds">The tools both chats may use.</param>
/// <param name="DataSourceOptions">The data sources both chats may search, and how they search them.</param>
/// <param name="MissingParts">The parts the new series cannot take over.</param>
public sealed record ModelComparisonSeriesCopy(
    AIStudio.Settings.Provider First,
    AIStudio.Settings.Provider Second,
    bool JudgeEnabled,
    AIStudio.Settings.Provider Judge,
    Profile Profile,
    HashSet<string> ToolIds,
    DataSourceOptions DataSourceOptions,
    IReadOnlyList<ModelComparisonSeriesPart> MissingParts)
{
    /// <summary>
    /// Takes over the setup of a series, as far as it is still available.
    /// </summary>
    /// <param name="settingsManager">The settings.</param>
    /// <param name="series">The series whose setup the new one takes over.</param>
    /// <param name="selectableToolIds">The tools of the series which the user could still select, see
    /// ToolRegistry.FilterSelectableToolIdsAsync.</param>
    /// <returns>The setup of the new series.</returns>
    public static ModelComparisonSeriesCopy Of(SettingsManager settingsManager, ModelComparisonSeries series, IReadOnlySet<string> selectableToolIds)
    {
        List<ModelComparisonSeriesPart> missingParts = [];

        var first = UsableProvider(settingsManager, series.FirstProviderId, series.First);
        if (first == AIStudio.Settings.Provider.NONE)
            missingParts.Add(ModelComparisonSeriesPart.FIRST_MODEL);

        var second = UsableProvider(settingsManager, series.SecondProviderId, series.Second);
        if (second == AIStudio.Settings.Provider.NONE)
            missingParts.Add(ModelComparisonSeriesPart.SECOND_MODEL);

        // The user wanted a judge, so the switch stays on, and the dialog asks for another one:
        var judgeEnabled = series.Judge is not null && !string.IsNullOrWhiteSpace(series.JudgeProviderId);
        var judge = series.Judge is { } judgeIdentity && judgeEnabled
            ? UsableProvider(settingsManager, series.JudgeProviderId, judgeIdentity)
            : AIStudio.Settings.Provider.NONE;

        if (judgeEnabled && judge == AIStudio.Settings.Provider.NONE)
            missingParts.Add(ModelComparisonSeriesPart.JUDGE);

        var profile = settingsManager.GetProfileById(series.ProfileId);
        var hadProfile = !string.IsNullOrWhiteSpace(series.ProfileId) && !string.Equals(series.ProfileId, Profile.NO_PROFILE.Id, StringComparison.OrdinalIgnoreCase);
        if (hadProfile && profile == Profile.NO_PROFILE)
            missingParts.Add(ModelComparisonSeriesPart.PROFILE);

        var toolIds = series.ToolIds.Where(selectableToolIds.Contains).ToHashSet(StringComparer.Ordinal);
        if (toolIds.Count < series.ToolIds.Distinct(StringComparer.Ordinal).Count())
            missingParts.Add(ModelComparisonSeriesPart.TOOLS);

        List<string> dataSourceIds = PreviewFeatures.PRE_RAG_2024.IsEnabled(settingsManager)
            ? [.. series.DataSourceIds.Where(dataSourceId => settingsManager.ConfigurationData.DataSources.Any(dataSource => dataSource.Id == dataSourceId))]
            : [];

        if (dataSourceIds.Count < series.DataSourceIds.Count)
            missingParts.Add(ModelComparisonSeriesPart.DATA_SOURCES);

        var dataSourceOptions = new DataSourceOptions
        {
            DisableDataSources = dataSourceIds.Count == 0,
            AutomaticDataSourceSelection = false,
            AutomaticValidation = false,
            RetrievalMode = series.RetrievalMode ?? DataSourceRetrievalMode.SEMANTIC_SEARCH,
            PreselectedDataSourceIds = dataSourceIds,
        };

        return new(first, second, judgeEnabled, judge, profile, toolIds, dataSourceOptions, missingParts);
    }

    /// <summary>
    /// Gets the provider of a model of the series, as long as it still uses the same model and the assistant may use it.
    /// </summary>
    private static AIStudio.Settings.Provider UsableProvider(SettingsManager settingsManager, string providerId, ModelComparisonModelIdentity identity)
    {
        var provider = ModelComparisonProviderCheck.ProviderAsItWas(settingsManager, providerId, identity);
        return ModelComparisonProviderCheck.IssueOf(settingsManager, provider) is null
            ? provider
            : AIStudio.Settings.Provider.NONE;
    }
}