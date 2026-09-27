namespace AIStudio.Tools.RAG;

/// <summary>
/// What kept a search from covering the whole data source.
/// </summary>
public enum RetrievalGap
{
    /// <summary>
    /// The data source could not be searched at all, e.g., while it is being indexed again, or
    /// when its ERI server could not be reached.
    /// </summary>
    NOT_SEARCHED,

    /// <summary>
    /// Part of the search failed, e.g., the vector search while the embedding provider is not
    /// available. The matches came from the rest of it and may be incomplete.
    /// </summary>
    PARTLY_SEARCHED,

    /// <summary>
    /// The query could not be used for part of the search, e.g., because it is longer than the
    /// embedding model accepts. A shorter query would be searched in full.
    /// </summary>
    QUERY_NOT_SEARCHABLE,
}