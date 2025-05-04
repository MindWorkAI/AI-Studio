using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG.AugmentationProcesses;
using AIStudio.Tools.RAG.DataSourceSelectionProcesses;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.RAG.RAGProcesses;

public sealed class AISrcSelWithRetCtxVal : IRagProcess
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AISrcSelWithRetCtxVal).Namespace, nameof(AISrcSelWithRetCtxVal));
    
    #region Implementation of IRagProcess

    /// <inheritdoc />
    public string TechnicalName => "AISrcSelWithRetCtxVal";

    /// <inheritdoc />
    public string UIName => TB("AI source selection with AI retrieval context validation");
    
    /// <inheritdoc />
    public string Description => TB("This RAG process filters data sources, automatically selects appropriate sources, optionally allows manual source selection, retrieves data, and automatically validates the retrieval context.");

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
            // We read the last block in the chat thread. We need to re-arrange
            // the order of blocks later, after the augmentation process takes
            // place:
            //
            if(chatThread.Blocks.Count == 0)
            {
                logger.LogError("The chat thread is empty. Skipping the RAG process.");
                return chatThread;
            }
            
            if (chatThread.Blocks.Last().Role != ChatRole.AI)
            {
                logger.LogError("The last block in the chat thread is not the AI block. There is something wrong with the chat thread. Skipping the RAG process.");
                return chatThread;
            }
            
            //
            // At this point in time, the chat thread contains already the
            // last block, which is the waiting AI block. We need to remove
            // this block before we call some parts of the RAG process:
            //
            var chatThreadWithoutWaitingAIBlock = chatThread with { Blocks = chatThread.Blocks[..^1] };
            
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
            else
            {
                var previousDataSecurity = chatThread.DataSecurity;
                
                //
                // Update the data security of the chat thread. We consider the current data security
                // of the chat thread and the data security of the selected data sources:
                //
                var dataSecurityRestrictedToSelfHosted = selectedDataSources.Any(x => x.SecurityPolicy is DataSourceSecurity.SELF_HOSTED);
                chatThread.DataSecurity = dataSecurityRestrictedToSelfHosted switch
                {
                    //
                    //
                    // Case: the data sources which are selected have a security policy
                    // of SELF_HOSTED (at least one data source).
                    //
                    // When the policy was already set to ALLOW_ANY, we restrict it
                    // to SELF_HOSTED.
                    //
                    true => DataSourceSecurity.SELF_HOSTED,
                    
                    //
                    // Case: the data sources which are selected have a security policy
                    // of ALLOW_ANY (none of the data sources has a SELF_HOSTED policy).
                    //
                    // When the policy was already set to SELF_HOSTED, we must keep that.
                    //
                    false => chatThread.DataSecurity switch
                    {
                        //
                        // When the policy was not specified yet, we set it to ALLOW_ANY.
                        //
                        DataSourceSecurity.NOT_SPECIFIED => DataSourceSecurity.ALLOW_ANY,
                        DataSourceSecurity.ALLOW_ANY => DataSourceSecurity.ALLOW_ANY,
                        
                        //
                        // When the policy was already set to SELF_HOSTED, we must keep that.
                        // This is important since the thread might already contain data
                        // from a data source with a SELF_HOSTED policy.
                        //
                        DataSourceSecurity.SELF_HOSTED => DataSourceSecurity.SELF_HOSTED,
                        
                        // Default case: we use the current data security of the chat thread.
                        _ => chatThread.DataSecurity,
                    }
                };
                
                if (previousDataSecurity != chatThread.DataSecurity)
                    logger.LogInformation($"The data security of the chat thread was updated from '{previousDataSecurity}' to '{chatThread.DataSecurity}'.");
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
                    retrievalTasks.Add(dataSource.RetrieveDataAsync(lastPrompt, chatThreadWithoutWaitingAIBlock, token));
            
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
                var augmentationProcess = new AugmentationOne();
                chatThread = await augmentationProcess.ProcessAsync(provider, lastPrompt, chatThread, dataContexts, token);
            }
        }
        
        return chatThread;
    }

    #endregion
}