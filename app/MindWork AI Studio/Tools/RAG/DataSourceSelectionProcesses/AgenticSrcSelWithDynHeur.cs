using AIStudio.Agents;
using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.RAG.DataSourceSelectionProcesses;

public class AgenticSrcSelWithDynHeur : IDataSourceSelectionProcess
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AgenticSrcSelWithDynHeur).Namespace, nameof(AgenticSrcSelWithDynHeur));
    
    #region Implementation of IDataSourceSelectionProcess

    /// <inheritdoc />
    public string TechnicalName => "AgenticSrcSelWithDynHeur";

    /// <inheritdoc />
    public string UIName => TB("Automatic AI data source selection with heuristik source reduction");

    /// <inheritdoc />
    public string Description => TB("Automatically selects the appropriate data sources based on the last prompt. Applies a heuristic reduction at the end to reduce the number of data sources.");
    
    /// <inheritdoc />
    public async Task<DataSelectionResult> SelectDataSourcesAsync(IProvider provider, IContent lastPrompt, ChatThread chatThread, AllowedSelectedDataSources dataSources, CancellationToken token = default)
    {
        var proceedWithRAG = true;
        IReadOnlyList<IDataSource> selectedDataSources = [];
        IReadOnlyList<DataSourceAgentSelected> finalAISelection = [];
        
        // Get the logger:
        var logger = Program.SERVICE_PROVIDER.GetService<ILogger<AgenticSrcSelWithDynHeur>>()!;
        
        // Get the settings manager:
        var settings = Program.SERVICE_PROVIDER.GetService<SettingsManager>()!;
        
        // Get the agent for the data source selection:
        var selectionAgent = Program.SERVICE_PROVIDER.GetService<AgentDataSourceSelection>()!;

        try
        {
            // Let the AI agent do its work:
            var aiSelectedDataSources = await selectionAgent.PerformSelectionAsync(provider, lastPrompt, chatThread, dataSources, token);

            // Check if the AI selected any data sources:
            if (aiSelectedDataSources.Count is 0)
            {
                logger.LogWarning("The AI did not select any data sources. The RAG process is skipped.");
                proceedWithRAG = false;
                
                return new(proceedWithRAG, selectedDataSources);
            }

            // Log the selected data sources:
            var selectedDataSourceInfo = aiSelectedDataSources.Select(ds => $"[Id={ds.Id}, reason={ds.Reason}, confidence={ds.Confidence}]").Aggregate((a, b) => $"'{a}', '{b}'");
            logger.LogInformation($"The AI selected the data sources automatically. {aiSelectedDataSources.Count} data source(s) are selected: {selectedDataSourceInfo}.");

            //
            // Check how many data sources were hallucinated by the AI:
            //
            var totalAISelectedDataSources = aiSelectedDataSources.Count;

            // Filter out the data sources that are not available:
            aiSelectedDataSources = aiSelectedDataSources.Where(x => settings.ConfigurationData.DataSources.FirstOrDefault(ds => ds.Id == x.Id) is not null).ToList();

            // Store the real AI-selected data sources:
            finalAISelection = aiSelectedDataSources.Select(x => new DataSourceAgentSelected { DataSource = settings.ConfigurationData.DataSources.First(ds => ds.Id == x.Id), AIDecision = x, Selected = false }).ToList();

            var numHallucinatedSources = totalAISelectedDataSources - aiSelectedDataSources.Count;
            if (numHallucinatedSources > 0)
                logger.LogWarning($"The AI hallucinated {numHallucinatedSources} data source(s). We ignore them.");

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

                logger.LogInformation($"The AI selected {aiSelectedDataSources.Count} data source(s) with a confidence of at least {threshold}.");

                // Transform the final data sources to the actual data sources:
                selectedDataSources = aiSelectedDataSources.Select(x => settings.ConfigurationData.DataSources.FirstOrDefault(ds => ds.Id == x.Id)).Where(ds => ds is not null).ToList()!;
                return new(proceedWithRAG, selectedDataSources);
            }

            //
            // Case: we have max. 3 data sources. We take all of them:
            //

            // Transform the selected data sources to the actual data sources:
            selectedDataSources = aiSelectedDataSources.Select(x => settings.ConfigurationData.DataSources.FirstOrDefault(ds => ds.Id == x.Id)).Where(ds => ds is not null).ToList()!;

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
}