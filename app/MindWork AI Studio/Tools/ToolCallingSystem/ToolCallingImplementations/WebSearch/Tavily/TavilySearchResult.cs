namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Tavily;

/// <summary>
/// One hit of a Tavily search.
/// </summary>
/// <remarks>
/// Tavily returns its hits already ranked and adds the relevance score it ranked them by, plus
/// an identifier and a favicon. Nothing here re-sorts them: unlike a SearXNG instance, Tavily
/// merges no engines whose rankings would have to be weighed against each other. For its
/// general search Tavily reports no publication date.
/// </remarks>
internal sealed record TavilySearchResult
{
    public string Title { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// The excerpt of the page that matched, which is what other services call a snippet.
    /// </summary>
    public string Content { get; init; } = string.Empty;
}