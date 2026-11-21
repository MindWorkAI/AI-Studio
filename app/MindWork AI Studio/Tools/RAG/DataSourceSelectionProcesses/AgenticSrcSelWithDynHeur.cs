using AIStudio.AIOperations;
using AIStudio.AIOperations.DataSourceChoice;
using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.RAG.DataSourceSelectionProcesses;

public class AgenticSrcSelWithDynHeur : IDataSourceSelectionProcess
{
    private static readonly ILogger<AgenticSrcSelWithDynHeur> LOGGER = Program.LOGGER_FACTORY.CreateLogger<AgenticSrcSelWithDynHeur>();
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AgenticSrcSelWithDynHeur).Namespace, nameof(AgenticSrcSelWithDynHeur));
    
    #region Implementation of IDataSourceSelectionProcess

    /// <inheritdoc />
    public string TechnicalName => "AgenticSrcSelWithDynHeur";

    /// <inheritdoc />
    public string UIName => TB("Automatic AI data source selection with heuristik source reduction");

    /// <inheritdoc />
    public string Description => TB("Automatically selects the appropriate data sources based on the last prompt. Applies a heuristic reduction at the end to reduce the number of data sources.");
    
    /// <inheritdoc />
    public async Task<DataSelectionResult> SelectDataSourcesAsync(IProvider provider, IContent lastUserPrompt, ChatThread chatThread, AllowedSelectedDataSources dataSources, CancellationToken token = default)
    {
        var proceedWithRAG = true;
        IReadOnlyList<IDataSource> selectedDataSources = [];
        IReadOnlyList<DataSourceAgentSelected> finalAISelection = [];
        
        var choiceOperation = Program.SERVICE_PROVIDER.GetRequiredService<DataSourceChoiceOperation>();
        var providerResolver = Program.SERVICE_PROVIDER.GetRequiredService<AIOperationProviderResolver>();
        var settingsManager = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
        var allowedDataSources = dataSources.AllowedDataSources.ToDictionary(ds => ds.Id, StringComparer.Ordinal);

        try
        {
            //
            // Which provider chooses? Only one which may see the data sources: the model reads their
            // descriptions, and what it chooses decides what is searched.
            //
            var request = new DataSourceChoiceRequest(chatThread.SystemPrompt, lastUserPrompt, dataSources.AllowedDataSources);
            var choice = providerResolver.Resolve(choiceOperation, request, DataSourceChoiceOperation.GetProviderSettings(settingsManager), GetChoiceProviderOptions(settingsManager, provider.ConfiguredProviderId, dataSources.AllowedDataSources));
            if (!choice.HasProvider)
            {
                LOGGER.LogWarning($"No provider may select the data sources, which require data security '{dataSources.AllowedDataSources.GetRequiredSecurityPolicy()}' and provider confidence '{dataSources.AllowedDataSources.GetRequiredConfidenceLevel().Name}'. The RAG process is skipped.");
                proceedWithRAG = false;

                return new(proceedWithRAG, selectedDataSources);
            }

            // Let the AI do its work:
            var choiceResult = await choiceOperation.RunAsync(request, new(choice.Provider), token);
            if (!choiceResult.IsAnswered)
            {
                LOGGER.LogWarning("The AI gave no usable answer to the selection of data sources. The RAG process is skipped.");
                proceedWithRAG = false;

                return new(proceedWithRAG, selectedDataSources);
            }

            var aiSelectedDataSources = choiceResult.SelectedDataSources.ToList();

            // Check if the AI selected any data sources:
            if (aiSelectedDataSources.Count is 0)
            {
                LOGGER.LogWarning("The AI did not select any data sources. The RAG process is skipped.");
                proceedWithRAG = false;
                
                return new(proceedWithRAG, selectedDataSources);
            }

            // Log the selected data sources:
            var selectedDataSourceInfo = string.Join(", ", aiSelectedDataSources.Select(ds => $"'[Id={ds.Id}, reason={ds.Reason}, confidence={ds.Confidence}]'"));
            LOGGER.LogInformation($"The AI selected the data sources automatically. {aiSelectedDataSources.Count} data source(s) are selected: {selectedDataSourceInfo}.");

            //
            // Check how many data sources were hallucinated by the AI:
            //
            var totalAISelectedDataSources = aiSelectedDataSources.Count;

            // Filter out the data sources that are not available:
            aiSelectedDataSources = aiSelectedDataSources.Where(x => allowedDataSources.ContainsKey(x.Id)).ToList();

            // Store the real AI-selected data sources:
            finalAISelection = aiSelectedDataSources.Select(x => new DataSourceAgentSelected { DataSource = allowedDataSources[x.Id], AIDecision = x, Selected = false }).ToList();

            var numHallucinatedSources = totalAISelectedDataSources - aiSelectedDataSources.Count;
            if (numHallucinatedSources > 0)
                LOGGER.LogWarning($"The AI selected {numHallucinatedSources} unavailable data source(s). We ignore them.");

            if (aiSelectedDataSources.Count > 3)
            {
                // We have more than 3 data sources. Let's filter by confidence:
                var targetWindow = aiSelectedDataSources.DetermineTargetWindow(TargetWindowStrategy.A_FEW_GOOD_ONES);
                var threshold = aiSelectedDataSources.GetConfidenceThreshold(targetWindow);

                //
                // Filter the data sources by the threshold:
                //
                aiSelectedDataSources = aiSelectedDataSources.Where(x => x.Confidence >= threshold).ToList();
                foreach (var dataSource in finalAISelection)
                    if (aiSelectedDataSources.Any(x => x.Id == dataSource.DataSource.Id))
                        dataSource.Selected = true;

                LOGGER.LogInformation($"The AI selected {aiSelectedDataSources.Count} data source(s) with a confidence of at least {threshold}.");

                // Transform the final data sources to the actual data sources:
                selectedDataSources = aiSelectedDataSources.Select(x => allowedDataSources[x.Id]).ToList();
                return new(proceedWithRAG, selectedDataSources);
            }

            //
            // Case: we have max. 3 data sources. We take all of them:
            //

            // Transform the selected data sources to the actual data sources:
            selectedDataSources = aiSelectedDataSources.Select(x => allowedDataSources[x.Id]).ToList();

            // Mark the data sources as selected:
            foreach (var dataSource in finalAISelection)
                dataSource.Selected = true;

            return new(proceedWithRAG, selectedDataSources);
        }
        finally
        {
            // Send the selected data sources to the data source selection component.
            // Then, the user can see which data sources were selected by the AI.
            await MessageBus.INSTANCE.SendMessage(null, Event.RAG_AUTO_DATA_SOURCES_SELECTED, finalAISelection);
            chatThread.AISelectedDataSources = finalAISelection;
        }
    }

    #endregion

    /// <summary>
    /// What the process adds to the choice of the provider which selects the data sources.
    /// </summary>
    /// <remarks>
    /// Besides the fallbacks of the classic RAG process, the provider has to be allowed to see the data
    /// sources, by their security and by the confidence they require.
    /// </remarks>
    /// <param name="settingsManager">The settings.</param>
    /// <param name="chatProviderId">The ID of the provider of the chat.</param>
    /// <param name="dataSources">The data sources to choose from.</param>
    /// <returns>The options for the provider resolver.</returns>
    internal static AIOperationProviderOptions GetChoiceProviderOptions(SettingsManager settingsManager, string chatProviderId, IReadOnlyList<IDataSource> dataSources)
    {
        var requiredDataSecurity = dataSources.GetRequiredSecurityPolicy();
        var requiredConfidenceLevel = dataSources.GetRequiredConfidenceLevel();
        return ClassicRagProviderOptions.For(settingsManager, chatProviderId, candidate => candidate.AllowsDataSourceAccess(settingsManager, requiredDataSecurity, requiredConfidenceLevel)
            ? AIOperationProviderIssue.NONE
            : AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT);
    }
}