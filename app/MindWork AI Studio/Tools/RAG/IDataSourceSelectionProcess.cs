using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Tools.RAG;

public interface IDataSourceSelectionProcess
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
    /// Starts the data source selection process.
    /// </summary>
    /// <param name="provider">The LLM provider. Used as default for data selection agents.</param>
    /// <param name="lastPrompt">The last prompt that was issued by the user.</param>
    /// <param name="chatThread">The chat thread.</param>
    /// <param name="dataSources">The allowed data sources yielded by the data source service.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns></returns>
    public Task<DataSelectionResult> SelectDataSourcesAsync(IProvider provider, IContent lastPrompt, ChatThread chatThread, AllowedSelectedDataSources dataSources, CancellationToken token = default);
}