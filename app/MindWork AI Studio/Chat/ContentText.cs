using System.Text.Json.Serialization;

using AIStudio.Agents;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Services;

namespace AIStudio.Chat;

/// <summary>
/// Text content in the chat.
/// </summary>
public sealed class ContentText : IContent
{
    /// <summary>
    /// The minimum time between two streaming events, when the user
    /// enables the energy saving mode.
    /// </summary>
    private static readonly TimeSpan MIN_TIME = TimeSpan.FromSeconds(3);

    #region Implementation of IContent

    /// <inheritdoc />
    [JsonIgnore]
    public bool InitialRemoteWait { get; set; }

    /// <inheritdoc />
    // [JsonIgnore]
    public bool IsStreaming { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingDone { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    [JsonIgnore]
    public Func<Task> StreamingEvent { get; set; } = () => Task.CompletedTask;

    /// <inheritdoc />
    public async Task CreateFromProviderAsync(IProvider provider, SettingsManager settings, DataSourceService dataSourceService, Model chatModel, IContent? lastPrompt, ChatThread? chatThread, CancellationToken token = default)
    {
        if(chatThread is null)
            return;

        var logger = Program.SERVICE_PROVIDER.GetService<ILogger<ContentText>>()!;
        
        //
        // 1. Check if the user wants to bind any data sources to the chat:
        //
        if (chatThread.DataSourceOptions.IsEnabled() && lastPrompt is not null)
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
                // Get the agent for the data source selection:
                var selectionAgent = Program.SERVICE_PROVIDER.GetService<AgentDataSourceSelection>()!;
                
                // Let the AI agent do its work:
                var aiSelectedDataSources = await selectionAgent.PerformSelectionAsync(provider, lastPrompt, chatThread, dataSources, token);

                // Check if the AI selected any data sources:
                if(aiSelectedDataSources.Count is 0)
                {
                    logger.LogWarning("The AI did not select any data sources. The RAG process is skipped.");
                    proceedWithRAG = false;
                }
                else
                {
                    // Log the selected data sources:
                    var selectedDataSourceInfo = aiSelectedDataSources.Select(ds => $"[Id={ds.Id}, reason={ds.Reason}, confidence={ds.Confidence}]").Aggregate((a, b) => $"'{a}', '{b}'");
                    logger.LogInformation($"The AI selected the data sources automatically. {aiSelectedDataSources.Count} data source(s) are selected: {selectedDataSourceInfo}.");

                    //
                    // Check how many data sources were hallucinated by the AI:
                    //
                    var totalAISelectedDataSources = aiSelectedDataSources.Count;
                    
                    // Filter out the data sources that are not available:
                    aiSelectedDataSources = aiSelectedDataSources.Where(x => settings.ConfigurationData.DataSources.FirstOrDefault(ds => ds.Id == x.Id) is not null).ToList();
                    
                    var numHallucinatedSources = totalAISelectedDataSources - aiSelectedDataSources.Count;
                    if(numHallucinatedSources > 0)
                        logger.LogWarning($"The AI hallucinated {numHallucinatedSources} data source(s). We ignore them.");
                    
                    if (aiSelectedDataSources.Count > 3)
                    {
                        //
                        // We have more than 3 data sources. Let's filter by confidence.
                        // In order to do that, we must identify the lower and upper
                        // bounds of the confidence interval:
                        //
                        var confidenceValues = aiSelectedDataSources.Select(x => x.Confidence).ToList();
                        var lowerBound = confidenceValues.Min();
                        var upperBound = confidenceValues.Max();
                        
                        //
                        // Next, we search for a threshold so that we have between 2 and 3
                        // data sources. When not possible, we take all data sources.
                        //
                        var threshold = 0.0f;
                        
                        // Check the case where the confidence values are too close:
                        if (upperBound - lowerBound >= 0.01)
                        {
                            var previousThreshold = 0.0f;
                            for (var i = 0; i < 10; i++)
                            {
                                threshold = lowerBound + (upperBound - lowerBound) * i / 10;
                                var numMatches = aiSelectedDataSources.Count(x => x.Confidence >= threshold);
                                if (numMatches <= 1)
                                {
                                    threshold = previousThreshold;
                                    break;
                                }
		
                                if (numMatches is <= 3 and >= 2)
                                    break;
                                
                                previousThreshold = threshold;
                            }
                        }
                        
                        //
                        // Filter the data sources by the threshold:
                        //
                        aiSelectedDataSources = aiSelectedDataSources.Where(x => x.Confidence >= threshold).ToList();
                        logger.LogInformation($"The AI selected {aiSelectedDataSources.Count} data source(s) with a confidence of at least {threshold}.");
                        
                        // Transform the final data sources to the actual data sources:
                        selectedDataSources = aiSelectedDataSources.Select(x => settings.ConfigurationData.DataSources.FirstOrDefault(ds => ds.Id == x.Id)).Where(ds => ds is not null).ToList()!;
                    }
                    
                    // We have max. 3 data sources. We take all of them:
                    else
                    {
                        // Transform the selected data sources to the actual data sources.
                        selectedDataSources = aiSelectedDataSources.Select(x => settings.ConfigurationData.DataSources.FirstOrDefault(ds => ds.Id == x.Id)).Where(ds => ds is not null).ToList()!;
                    }
                }
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
            if (proceedWithRAG)
            {
                
            }

            //
            // Perform the augmentation of the R(A)G process:
            //
            if (proceedWithRAG)
            {
                
            }
        }
        
        // Store the last time we got a response. We use this later
        // to determine whether we should notify the UI about the
        // new content or not. Depends on the energy saving mode
        // the user chose.
        var last = DateTimeOffset.Now;

        // Start another thread by using a task to uncouple
        // the UI thread from the AI processing:
        await Task.Run(async () =>
        {
            // We show the waiting animation until we get the first response:
            this.InitialRemoteWait = true;
            
            // Iterate over the responses from the AI:
            await foreach (var deltaText in provider.StreamChatCompletion(chatModel, chatThread, settings, token))
            {
                // When the user cancels the request, we stop the loop:
                if (token.IsCancellationRequested)
                    break;

                // Stop the waiting animation:
                this.InitialRemoteWait = false;
                this.IsStreaming = true;
                
                // Add the response to the text:
                this.Text += deltaText;
                
                // Notify the UI that the content has changed,
                // depending on the energy saving mode:
                var now = DateTimeOffset.Now;
                switch (settings.ConfigurationData.App.IsSavingEnergy)
                {
                    // Energy saving mode is off. We notify the UI
                    // as fast as possible -- no matter the odds:
                    case false:
                        await this.StreamingEvent();
                        break;
                    
                    // Energy saving mode is on. We notify the UI
                    // only when the time between two events is
                    // greater than the minimum time:
                    case true when now - last > MIN_TIME:
                        last = now;
                        await this.StreamingEvent();
                        break;
                }
            }

            // Stop the waiting animation (in case the loop
            // was stopped, or no content was received):
            this.InitialRemoteWait = false;
            this.IsStreaming = false;
        }, token);
        
        // Inform the UI that the streaming is done:
        await this.StreamingDone();
    }

    #endregion
    
    /// <summary>
    /// The text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}