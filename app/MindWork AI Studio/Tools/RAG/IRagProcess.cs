using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Tools.RAG;

public interface IRagProcess
{
    /// <summary>
    /// How is the RAG process called?
    /// </summary>
    public string TechnicalName { get; }

    /// <summary>
    /// How is the RAG process called in the UI?
    /// </summary>
    public string UIName { get; }

    /// <summary>
    /// How works the RAG process?
    /// </summary>
    public string Description { get; }
    
    /// <summary>
    /// Starts the RAG process.
    /// </summary>
    /// <param name="provider">The LLM provider. Used to check whether the data sources are allowed to be used by this LLM.</param>
    /// <param name="lastPrompt">The last prompt that was issued by the user.</param>
    /// <param name="chatThread">The chat thread.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The altered chat thread.</returns>
    public Task<ChatThread> ProcessAsync(IProvider provider, IContent lastPrompt, ChatThread chatThread, CancellationToken token = default);
}