using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.RAG.DataSourceSelectionProcesses;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.RAG.RAGProcesses;

public sealed class AISrcSelWithRetCtxVal : IRagProcess
{
    #region Implementation of IRagProcess

    /// <inheritdoc />
    public string TechnicalName => "AISrcSelWithRetCtxVal";

    /// <inheritdoc />
    public string UIName => "AI source selection with AI retrieval context validation";
    
    /// <inheritdoc />
    public string Description => "This RAG process filters data sources, automatically selects appropriate sources, optionally allows manual source selection, retrieves data, and automatically validates the retrieval context.";

    /// <inheritdoc />
    public async Task<ChatThread> ProcessAsync(IProvider provider, IContent lastPrompt, ChatThread chatThread, CancellationToken token = default)
    {
        var logger = Program.SERVICE_PROVIDER.GetService<ILogger<AISrcSelWithRetCtxVal>>()!;
        var settings = Program.SERVICE_PROVIDER.GetService<SettingsManager>()!;
        var dataSourceService = Program.SERVICE_PROVIDER.GetService<DataSourceService>()!;
        
        //
        // 1. Check if the user wants to bind any data sources to the chat:
        //
        if (chatThread.DataSourceOptions.IsEnabled())
        {
            logger.LogInformation("Data sources are enabled for this chat.");
            
            // Across the different code-branches, we keep track of whether it
            // makes sense to proceed with the RAG process:
            var proceedWithRAG = true;
            
            //
            // When the user wants to bind data sources to the chat, we
            // have to check if the data sources are available for the
            // selected provider. Also, we have to check if any ERI
            // data sources changed its security requirements.
            //
            List<IDataSource> preselectedDataSources = chatThread.DataSourceOptions.PreselectedDataSourceIds.Select(id => settings.ConfigurationData.DataSources.FirstOrDefault(ds => ds.Id == id)).Where(ds => ds is not null).ToList()!;
            var dataSources = await dataSourceService.GetDataSources(provider, preselectedDataSources);
            var selectedDataSources = dataSources.SelectedDataSources;
            
            //
            // Should the AI select the data sources?
            //
            if (chatThread.DataSourceOptions.AutomaticDataSourceSelection)
            {
                var dataSourceSelectionProcess = new AgenticSrcSelWithDynHeur();
                var result = await dataSourceSelectionProcess.SelectDataSourcesAsync(provider, lastPrompt, chatThread, dataSources, token);
                proceedWithRAG = result.ProceedWithRAG;
                selectedDataSources = result.SelectedDataSources;
            }
            else
            {
                //
                // No, the user made the choice manually:
                //
                var selectedDataSourceInfo = selectedDataSources.Select(ds => ds.Name).Aggregate((a, b) => $"'{a}', '{b}'");
                logger.LogInformation($"The user selected the data sources manually. {selectedDataSources.Count} data source(s) are selected: {selectedDataSourceInfo}.");
            }

            if(selectedDataSources.Count == 0)
            {
                logger.LogWarning("No data sources are selected. The RAG process is skipped.");
                proceedWithRAG = false;
            }
            
            //
            // Trigger the retrieval part of the (R)AG process:
            //
            var dataContexts = new List<IRetrievalContext>();
            if (proceedWithRAG)
            {
                //
                // We kick off the retrieval process for each data source in parallel:
                //
                var retrievalTasks = new List<Task<IReadOnlyList<IRetrievalContext>>>(selectedDataSources.Count);
                foreach (var dataSource in selectedDataSources)
                    retrievalTasks.Add(dataSource.RetrieveDataAsync(lastPrompt, chatThread, token));
            
                //
                // Wait for all retrieval tasks to finish:
                //
                foreach (var retrievalTask in retrievalTasks)
                {
                    try
                    {
                        dataContexts.AddRange(await retrievalTask);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "An error occurred during the retrieval process.");
                    }
                }
            }

            //
            // Perform the augmentation of the R(A)G process:
            //
            if (proceedWithRAG)
            {
                
            }
        }
        
        return chatThread;
    }

    #endregion
}