using System.Text;

using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Tools.RAG.AugmentationProcesses;

public sealed class AugmentationOne : IAugmentationProcess
{
    #region Implementation of IAugmentationProcess

    /// <inheritdoc />
    public string TechnicalName => "AugmentationOne";
    
    /// <inheritdoc />
    public string UIName => "Standard augmentation process";
    
    /// <inheritdoc />
    public string Description => "This is the standard augmentation process, which uses all retrieval contexts to augment the chat thread.";
    
    /// <inheritdoc />
    public async Task<ChatThread> ProcessAsync(IProvider provider, IContent lastPrompt, ChatThread chatThread, IReadOnlyList<IRetrievalContext> retrievalContexts, CancellationToken token = default)
    {
        var logger = Program.SERVICE_PROVIDER.GetService<ILogger<AugmentationOne>>()!;
        if(retrievalContexts.Count == 0)
        {
            logger.LogWarning("No retrieval contexts were issued. Skipping the augmentation process.");
            return chatThread;
        }
        
        var numTotalRetrievalContexts = retrievalContexts.Count;
        logger.LogInformation($"Starting the augmentation process over {numTotalRetrievalContexts:###,###,###,###} retrieval contexts.");
        
        //
        // We build a huge prompt from all retrieval contexts:
        //
        var sb = new StringBuilder();
        sb.AppendLine("The following useful information will help you in processing the user prompt:");
        sb.AppendLine();
        
        var index = 0;
        foreach(var retrievalContext in retrievalContexts)
        {
            index++;
            sb.AppendLine($"# Retrieval context {index} of {numTotalRetrievalContexts}");
            sb.AppendLine($"Data source name: {retrievalContext.DataSourceName}");
            sb.AppendLine($"Content category: {retrievalContext.Category}");
            sb.AppendLine($"Content type: {retrievalContext.Type}");
            sb.AppendLine($"Content path: {retrievalContext.Path}");
            
            if(retrievalContext.Links.Count > 0)
            {
                sb.AppendLine("Additional links:");
                foreach(var link in retrievalContext.Links)
                    sb.AppendLine($"- {link}");
            }
            
            switch(retrievalContext)
            {
                case RetrievalTextContext textContext:
                    sb.AppendLine();
                    sb.AppendLine("Matched text content:");
                    sb.AppendLine("````");
                    sb.AppendLine(textContext.MatchedText);
                    sb.AppendLine("````");
                    
                    if(textContext.SurroundingContent.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("Surrounding text content:");
                        foreach(var surrounding in textContext.SurroundingContent)
                        {
                            sb.AppendLine();
                            sb.AppendLine("````");
                            sb.AppendLine(surrounding);
                            sb.AppendLine("````");
                        }
                    }
                    
                    
                    break;
                
                case RetrievalImageContext imageContext:
                    sb.AppendLine();
                    sb.AppendLine("Matched image content as base64-encoded data:");
                    sb.AppendLine("````");
                    sb.AppendLine(await imageContext.AsBase64(token));
                    sb.AppendLine("````");
                    break;
                
                default:
                    logger.LogWarning($"The retrieval content type '{retrievalContext.Type}' of data source '{retrievalContext.DataSourceName}' at location '{retrievalContext.Path}' is not supported yet.");
                    break;
            }
            
            sb.AppendLine();
        }
        
        //
        // Append the entire augmentation to the chat thread,
        // just before the user prompt:
        //
        chatThread.Blocks.Insert(chatThread.Blocks.Count - 1, new()
        {
            Role = ChatRole.RAG,
            Time = DateTimeOffset.UtcNow,
            ContentType = ContentType.TEXT,
            HideFromUser = true,
            Content = new ContentText
            {
                Text = sb.ToString(),
            }
        });
        
        return chatThread;
    }

    #endregion
}