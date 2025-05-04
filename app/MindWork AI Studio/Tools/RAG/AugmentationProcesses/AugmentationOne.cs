using System.Text;

using AIStudio.Agents;
using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.RAG.AugmentationProcesses;

public sealed class AugmentationOne : IAugmentationProcess
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AugmentationOne).Namespace, nameof(AugmentationOne));
    
    #region Implementation of IAugmentationProcess

    /// <inheritdoc />
    public string TechnicalName => "AugmentationOne";
    
    /// <inheritdoc />
    public string UIName => TB("Standard augmentation process");
    
    /// <inheritdoc />
    public string Description => TB("This is the standard augmentation process, which uses all retrieval contexts to augment the chat thread.");
    
    /// <inheritdoc />
    public async Task<ChatThread> ProcessAsync(IProvider provider, IContent lastPrompt, ChatThread chatThread, IReadOnlyList<IRetrievalContext> retrievalContexts, CancellationToken token = default)
    {
        var logger = Program.SERVICE_PROVIDER.GetService<ILogger<AugmentationOne>>()!;
        var settings = Program.SERVICE_PROVIDER.GetService<SettingsManager>()!;
        
        if(retrievalContexts.Count == 0)
        {
            logger.LogWarning("No retrieval contexts were issued. Skipping the augmentation process.");
            return chatThread;
        }

        var numTotalRetrievalContexts = retrievalContexts.Count;
        
        // Want the user to validate all retrieval contexts?
        if (settings.ConfigurationData.AgentRetrievalContextValidation.EnableRetrievalContextValidation && chatThread.DataSourceOptions.AutomaticValidation)
        {
            // Let's get the validation agent & set up its provider:
            var validationAgent = Program.SERVICE_PROVIDER.GetService<AgentRetrievalContextValidation>()!;
            validationAgent.SetLLMProvider(provider);
            
            // Let's validate all retrieval contexts:
            var validationResults = await validationAgent.ValidateRetrievalContextsAsync(lastPrompt, chatThread, retrievalContexts, token);
         
            //
            // Now, filter the retrieval contexts to the most relevant ones:
            //
            var targetWindow = validationResults.DetermineTargetWindow(TargetWindowStrategy.TOP10_BETTER_THAN_GUESSING);
            var threshold = validationResults.GetConfidenceThreshold(targetWindow);
            
            // Filter the retrieval contexts:
            retrievalContexts = validationResults.Where(x => x.RetrievalContext is not null && x.Confidence >= threshold).Select(x => x.RetrievalContext!).ToList();
        }
        
        logger.LogInformation($"Starting the augmentation process over {numTotalRetrievalContexts:###,###,###,###} retrieval contexts.");
        
        //
        // We build a huge prompt from all retrieval contexts:
        //
        var sb = new StringBuilder();
        sb.AppendLine("The following useful information will help you in processing the user prompt:");
        sb.AppendLine();
        
        // Let's convert all retrieval contexts to Markdown:
        await retrievalContexts.AsMarkdown(sb, token);
        
        // Add the augmented data to the chat thread:
        chatThread.AugmentedData = sb.ToString();
        return chatThread;
    }

    #endregion
}