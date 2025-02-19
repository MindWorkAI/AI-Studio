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
        
        // Let's convert all retrieval contexts to Markdown:
        await retrievalContexts.AsMarkdown(sb, token);
        
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