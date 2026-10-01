namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Tavily;

/// <summary>
/// What Tavily answers to a search.
/// </summary>
/// <remarks>
/// Declared down to what the tool reads. Tavily also returns how long the search took, an
/// identifier for the request, and the fields that were asked for through the parameters this
/// tool does not send.
/// </remarks>
internal sealed record TavilySearchResponse
{
    public IReadOnlyList<TavilySearchResult> Results { get; init; } = [];
}