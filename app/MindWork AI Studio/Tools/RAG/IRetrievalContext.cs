namespace AIStudio.Tools.RAG;

/// <summary>
/// The common interface for any retrieval context.
/// </summary>
public interface IRetrievalContext
{
    /// <summary>
    /// The name of the data source.
    /// </summary>
    /// <remarks>
    /// Depending on the configuration, the AI is selecting the appropriate data source.
    /// In order to inform the user about where the information is coming from, the data
    /// source name is necessary.
    /// </remarks>
    public string DataSourceName { get; init; }

    /// <summary>
    /// The category of the content, like e.g., text, audio, image, etc.
    /// </summary>
    public RetrievalContentCategory Category { get; init; }

    /// <summary>
    /// What type of content is being retrieved? Like e.g., a project proposal, spreadsheet, art, etc.
    /// </summary>
    public RetrievalContentType Type { get; init; }

    /// <summary>
    /// The path to the content, e.g., a URL, a file path, a path in a graph database, etc.
    /// </summary>
    public string Path { get; init; }

    /// <summary>
    /// Links to related content, e.g., links to Wikipedia articles, links to sources, etc.
    /// </summary>
    /// <remarks>
    /// Why would you need links for retrieval? You are right that not all retrieval
    /// contexts need links. But think about a web search feature, where we want to
    /// query a search engine and get back a list of links to the most relevant
    /// matches. Think about a continuous web crawler that is constantly looking for
    /// new information and adding it to the knowledge base. In these cases, links
    /// are essential.
    /// </remarks>
    public IReadOnlyList<string> Links { get; init; }
}