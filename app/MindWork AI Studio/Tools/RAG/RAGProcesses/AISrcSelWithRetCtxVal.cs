using System.Text;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG.AugmentationProcesses;
using AIStudio.Tools.RAG.DataSourceSelectionProcesses;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tools.RAG.RAGProcesses;

public sealed class AISrcSelWithRetCtxVal : IRagProcess
{
    private static readonly ILogger<AISrcSelWithRetCtxVal> LOGGER = Program.LOGGER_FACTORY.CreateLogger<AISrcSelWithRetCtxVal>();
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AISrcSelWithRetCtxVal).Namespace, nameof(AISrcSelWithRetCtxVal));
    
    #region Implementation of IRagProcess

    /// <inheritdoc />
    public string TechnicalName => "AISrcSelWithRetCtxVal";

    /// <inheritdoc />
    public string UIName => TB("AI source selection with AI retrieval context validation");
    
    /// <inheritdoc />
    public string Description => TB("This RAG process filters data sources, automatically selects appropriate sources, optionally allows manual source selection, retrieves data, and automatically validates the retrieval context.");

    /// <inheritdoc />
    public async Task<ChatThread> ProcessAsync(IProvider provider, Model chatModel, IContent lastUserPrompt, ChatThread chatThread, CancellationToken token = default)
    {
        var settings = Program.SERVICE_PROVIDER.GetService<SettingsManager>()!;
        var dataSourceService = Program.SERVICE_PROVIDER.GetService<DataSourceService>()!;
        
        //
        // What an earlier message retrieved must not travel along with this one. The augmented
        // data and the AI-selected data sources describe the last retrieval, not a certain
        // message, and only the steps below ever write them. Without starting from empty, every
        // message which retrieves nothing -- because the data sources were switched off, none of
        // them can be used right now, or the search found nothing -- would still send the
        // passages of the last message which did, cf. ChatThread.RollBackTo.
        //
        // The data security, the required provider confidence, and where the chat may still send
        // data stay as they are: they only ever tighten, because the data which raised them was
        // seen by this thread.
        //
        chatThread.AugmentedData = string.Empty;
        chatThread.AISelectedDataSources = [];

        //
        // 1. Check if the user wants to bind any data sources to the chat:
        //
        //
        // Data sources are a preview feature. The check belongs here rather than in the options
        // themselves: a chat keeps its data source options while the feature is switched off, and
        // organizations may preselect data sources through a configuration plugin. Without this,
        // such a chat would still run the entire RAG process with the feature disabled.
        //
        if (PreviewFeatures.PRE_RAG_2024.IsEnabled(settings) && chatThread.DataSourceOptions.IsEnabled())
        {
            LOGGER.LogInformation("Data sources are enabled for this chat.");

            //
            // When the model searches the data sources itself, AI Studio does not search them as
            // well. The reset above already dropped whatever an earlier message retrieved:
            //
            if (await IsSearchedByTheModelAsync(Program.SERVICE_PROVIDER.GetService<ToolRegistry>(), settings, provider, chatModel, chatThread))
            {
                LOGGER.LogInformation("The model searches the data sources of this chat itself, aka Semantic Search. Skipping the RAG process.");
                return chatThread;
            }

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
                LOGGER.LogError("The chat thread is empty. Skipping the RAG process.");
                return chatThread;
            }
            
            if (chatThread.Blocks.Last().Role != ChatRole.AI)
            {
                LOGGER.LogError("The last block in the chat thread is not the AI block. There is something wrong with the chat thread. Skipping the RAG process.");
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
            var dataSources = await dataSourceService.GetDataSources(provider, chatThread.DataSourceOptions, DataSourceRetrievalMode.EVERY_MESSAGE, preselectedDataSources);
            var selectedDataSources = dataSources.SelectedDataSources;
            
            //
            // Should the AI select the data sources?
            //
            if (chatThread.DataSourceOptions.AutomaticDataSourceSelection)
            {
                var dataSourceSelectionProcess = new AgenticSrcSelWithDynHeur();
                var result = await dataSourceSelectionProcess.SelectDataSourcesAsync(provider, lastUserPrompt, chatThread, dataSources, token);
                proceedWithRAG = result.ProceedWithRAG;
                selectedDataSources = result.SelectedDataSources;
            }
            else
            {
                //
                // No, the user made the choice manually:
                //
                var selectedDataSourceInfo = string.Join(", ", selectedDataSources.Select(ds => $"'{ds.Name}'"));
                LOGGER.LogInformation($"The user selected the data sources manually. {selectedDataSources.Count} data source(s) are selected: {selectedDataSourceInfo}.");
            }

            if(selectedDataSources.Count == 0)
            {
                //
                // Reaching this point means the user never saw a source of theirs selected: the
                // selection shows what survived the filters, so an empty result there is an empty
                // selection on screen as well. Telling them per answer that their sources were
                // lost would announce a loss they were never shown in the first place. This state
                // belongs into the selection instead, which names the preselected sources it
                // cannot use.
                //
                // An ERI data source whose server is out of reach is another matter: it was not held
                // back by a filter, it just could not answer this time. The model learns about it
                // below, after the augmentation.
                //
                LOGGER.LogWarning("No data sources are selected. The RAG process is skipped.");
                proceedWithRAG = false;
            }
            else
            {
                var previousDataSecurity = chatThread.DataSecurity;
                var previousRequiredProviderConfidence = chatThread.RequiredProviderConfidence;
                
                //
                // Update the data security of the chat thread. We consider the current data security
                // of the chat thread and the data security of the selected data sources: at least
                // one data source with a SELF_HOSTED policy restricts the chat to self-hosted
                // providers. A restriction set earlier stays either way, because the thread might
                // already contain data from a data source with a SELF_HOSTED policy:
                //
                var dataSecurityRestrictedToSelfHosted = selectedDataSources
                    .OfType<IExternalDataSource>()
                    .Any(dataSource => dataSource.SecurityPolicy is DataSourceSecurity.SELF_HOSTED);
                chatThread.RequireDataSecurity(dataSecurityRestrictedToSelfHosted ? DataSourceSecurity.SELF_HOSTED : DataSourceSecurity.ALLOW_ANY);
                
                if (previousDataSecurity != chatThread.DataSecurity)
                    LOGGER.LogInformation($"The data security of the chat thread was updated from '{previousDataSecurity}' to '{chatThread.DataSecurity}'.");

                foreach (var dataSource in selectedDataSources.OfType<IInternalDataSource>())
                    chatThread.RequireProviderConfidence(dataSource.ConfidenceLevel);

                if (previousRequiredProviderConfidence != chatThread.RequiredProviderConfidence)
                    LOGGER.LogInformation($"The required provider confidence of the chat thread was updated from '{previousRequiredProviderConfidence.GetName()}' to '{chatThread.RequiredProviderConfidence.GetName()}'.");
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
                    retrievalTasks.Add(dataSource.RetrieveDataAsync(lastUserPrompt, chatThreadWithoutWaitingAIBlock, token));
            
                //
                // Wait for all retrieval tasks to finish, and count the passages of each data source.
                // The tasks are in the order of the selected data sources:
                //
                var passageCounts = new int[retrievalTasks.Count];
                for (var index = 0; index < retrievalTasks.Count; index++)
                {
                    try
                    {
                        var retrievedContexts = await retrievalTasks[index];
                        dataContexts.AddRange(retrievedContexts);
                        passageCounts[index] = retrievedContexts.Count;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // The user stopped the answer, which is no error of the data source:
                        LOGGER.LogInformation("The retrieval process was cancelled.");
                    }
                    catch (Exception e)
                    {
                        LOGGER.LogError(e, "An error occurred during the retrieval process.");
                    }
                }

                //
                // Right after the retrieval, before the augmentation: the passages are in the chat
                // from here on, and a step below which ends early must not leave it unrestricted:
                //
                RequireOutboundDataRestriction(chatThread, selectedDataSources, passageCounts, settings.ConfigurationData.DataSourceSecurity.MinimumOutboundDataRestriction);
            }

            //
            // Perform the augmentation of the R(A)G process:
            //
            IReadOnlyList<PreparedRetrievalContext> usedContexts = [];
            if (proceedWithRAG)
            {
                var augmentationProcess = new AugmentationOne();
                var augmentation = await augmentationProcess.ProcessAsync(provider, lastUserPrompt, chatThread, dataContexts, token);
                chatThread = augmentation.ChatThread;
                usedContexts = augmentation.UsedContexts;
            }

            //
            // The data sources of the chat whose server could not be reached were never searched.
            // The model learns about them, so that it does not take their silence for finding
            // nothing. This holds even when nothing was retrieved, e.g., because every selected data
            // source is out of reach:
            //
            chatThread.AugmentedData = WithUnreachableDataSources(chatThread.AugmentedData, dataSources.GetUnreachableOfTheChat(chatThread.DataSourceOptions));

            //
            // Add the sources and passages of what the AI received:
            //
            
            // We know that the last block is the AI answer block (cf. check above):
            var aiAnswerBlock = chatThread.Blocks.Last();
            var aiAnswerContent = aiAnswerBlock.Content;
            
            // It should never happen that the AI answer block does not contain a content part.
            // Just in case, we check this:
            if(aiAnswerContent is null)
                return chatThread;

            AddWhatTheAIReceived(aiAnswerContent, usedContexts);
        }
        
        return chatThread;
    }

    #endregion

    /// <summary>
    /// Lists below the answer what the AI received: the sources and the passages of the retrieval
    /// contexts handed to it.
    /// </summary>
    /// <remarks>
    /// Only those: a passage which the check of which passages fit left out never reached the AI,
    /// and the answer does not rest on it. Only a text answer keeps passages; every answer keeps
    /// its sources.
    /// </remarks>
    /// <param name="aiAnswerContent">The content of the answer.</param>
    /// <param name="usedContexts">The retrieval contexts handed to the AI.</param>
    internal static void AddWhatTheAIReceived(IContent aiAnswerContent, IReadOnlyList<PreparedRetrievalContext> usedContexts)
    {
        var sources = usedContexts.SelectMany(context => context.Sources);
        if (aiAnswerContent is ContentText aiAnswerText)
            aiAnswerText.AddSources(sources, usedContexts.Select(context => context.Passage).OfType<SourcePassage>());
        else
            aiAnswerContent.Sources.MergeSources(sources);
    }

    /// <summary>
    /// Restricts where the chat may still send data, because of the passages the retrieval brought in.
    /// </summary>
    /// <remarks>
    /// Semantic Search asks the same place, DataSourceRequirements, so that both ways of searching
    /// demand the same of the same data sources. Only the data sources with passages count: one
    /// which found nothing leaves the chat as it was. A passage the validation agent drops later
    /// still counts, which is the safe side. The data security and the provider confidence were
    /// raised before the retrieval already, for every selected data source, because the agents see
    /// the passages as well; the data sources with passages are among them, so asking again would
    /// change nothing.<br/><br/>
    /// The answer which follows does not offer the web search any more: the request reads the
    /// restriction from this very thread when it puts its tools together, after this process, see
    /// ToolRegistry.GetRunnableToolsAsync.
    /// </remarks>
    /// <param name="chatThread">The chat thread.</param>
    /// <param name="selectedDataSources">The data sources searched.</param>
    /// <param name="passageCounts">How many passages each data source returned, in the same order.</param>
    /// <param name="organizationMinimum">The least strict outbound data restriction the organization allows.</param>
    internal static void RequireOutboundDataRestriction(ChatThread chatThread, IReadOnlyList<IDataSource> selectedDataSources, IReadOnlyList<int> passageCounts, OutboundDataRestriction organizationMinimum)
    {
        var previousRestriction = chatThread.RequiredOutboundDataRestriction;
        chatThread.RequireOutboundDataRestriction(DataSourceRequirements.Of(selectedDataSources, passageCounts, organizationMinimum).OutboundData);
        if (previousRestriction != chatThread.RequiredOutboundDataRestriction)
            LOGGER.LogInformation($"The outbound data restriction of the chat thread was updated from '{previousRestriction.Restriction}' to '{chatThread.RequiredOutboundDataRestriction.Restriction}' because of the data source with id '{chatThread.RequiredOutboundDataRestriction.Reference}'.");
    }

    /// <summary>
    /// Adds the note on the data sources whose server could not be reached to what the model reads along with the passages.
    /// </summary>
    /// <param name="augmentedData">What the augmentation wrote, possibly nothing.</param>
    /// <param name="unreachableDataSources">The data sources of the chat whose server could not be reached.</param>
    /// <returns>The augmented data followed by the note, or the augmented data alone when every server answered.</returns>
    internal static string WithUnreachableDataSources(string augmentedData, IReadOnlyList<IDataSource> unreachableDataSources)
    {
        if (unreachableDataSources.Count == 0)
            return augmentedData;

        var builder = new StringBuilder(augmentedData);
        if (builder.Length > 0)
            builder.AppendLine();

        UnreachableDataSources.AppendTo(builder, unreachableDataSources);
        return builder.ToString();
    }

    /// <summary>
    /// Whether the model searches the data sources of this chat itself, through the tool semantic_search.
    /// </summary>
    /// <remarks>
    /// The registry decides with the same checks, the same provider settings, and the same part of
    /// the app as the request does before it offers the tool, see BaseProvider. A wrong true would
    /// leave the chat searching nothing at all, since the RAG process stands back and the request
    /// offers no tool; a chat whose model cannot search itself therefore always gets false here.
    /// </remarks>
    /// <param name="toolRegistry">The tool registry, when there is one. Without it, the request offers no tools either.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="provider">The LLM provider which answers.</param>
    /// <param name="chatModel">The model which answers.</param>
    /// <param name="chatThread">The chat thread.</param>
    /// <returns>True when AI Studio must leave the searching to the model.</returns>
    internal static async Task<bool> IsSearchedByTheModelAsync(ToolRegistry? toolRegistry, SettingsManager settings, IProvider provider, Model chatModel, ChatThread chatThread)
    {
        if (toolRegistry is null || !chatThread.MayRunTools(settings))
            return false;

        var retrievalMode = await toolRegistry.GetEffectiveRetrievalModeAsync(chatThread.DataSourceOptions, provider.CreateSettingsProvider(chatModel), chatThread.RuntimeComponent);
        return retrievalMode.Mode is DataSourceRetrievalMode.SEMANTIC_SEARCH;
    }
}
