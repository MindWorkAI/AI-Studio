using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Tools.RAG;

public interface IAugmentationProcess
{
    /// <summary>
    /// How is the augmentation process called?
    /// </summary>
    public string TechnicalName { get; }

    /// <summary>
    /// How is the augmentation process called in the UI?
    /// </summary>
    public string UIName { get; }

    /// <summary>
    /// How works the augmentation process?
    /// </summary>
    public string Description { get; }
    
    /// <summary>
    /// Starts the augmentation process.
    /// </summary>
    /// <param name="provider">The LLM provider. Gets used, e.g., for automatic retrieval context validation.</param>
    /// <param name="lastPrompt">The last prompt that was issued by the user.</param>
    /// <param name="chatThread">The chat thread.</param>
    /// <param name="retrievalContexts">The retrieval contexts that were issued by the retrieval process.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The altered chat thread.</returns>
    public Task<ChatThread> ProcessAsync(IProvider provider, IContent lastPrompt, ChatThread chatThread, IReadOnlyList<IRetrievalContext> retrievalContexts, CancellationToken token = default);
}