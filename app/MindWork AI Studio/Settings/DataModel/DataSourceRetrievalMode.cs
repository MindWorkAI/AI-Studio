namespace AIStudio.Settings.DataModel;

/// <summary>
/// How the data sources of a chat are searched.
/// </summary>
public enum DataSourceRetrievalMode
{
    /// <summary>
    /// The model searches the data sources itself, through the tool semantic_search, whenever a
    /// question calls for it. No agent takes part: the chat model picks the data sources and
    /// judges what it found.
    /// </summary>
    SEMANTIC_SEARCH,

    /// <summary>
    /// AI Studio searches the data sources with every message, before the model answers. This is
    /// the classic RAG process, with its agents for selecting data sources and for validating what
    /// was found.
    /// </summary>
    EVERY_MESSAGE,
}