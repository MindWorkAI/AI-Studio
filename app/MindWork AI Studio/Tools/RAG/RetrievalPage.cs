namespace AIStudio.Tools.RAG;

/// <summary>
/// One page of what a search in a data source found.
/// </summary>
/// <remarks>
/// A page does not say how many matches there are in total, and it could not: a vector search has
/// no total, since every chunk matches, only less similar ones match less. What a page does say is
/// whether asking for the next one is worth it.
/// </remarks>
/// <param name="Contexts">What this page found, the most relevant first.</param>
/// <param name="HasMore">True when the next page can be retrieved and may hold further matches. That
/// page can still turn out empty, when everything on it was already shown on an earlier page. False
/// when the search is exhausted, or when this page is the last one which can be retrieved at all, cf.
/// RetrievalPaging.GetLastPage.</param>
public sealed record RetrievalPage(IReadOnlyList<IRetrievalContext> Contexts, bool HasMore)
{
    /// <summary>
    /// A page without any matches and nothing after it.
    /// </summary>
    public static readonly RetrievalPage EMPTY = new([], false);

    /// <summary>
    /// What kept the search from covering the whole data source. Empty when nothing did.
    /// </summary>
    /// <remarks>
    /// Without this, a data source which could not be searched would look like one which found
    /// nothing, and the model would tell the user their documents do not mention what they might
    /// well mention. A local data source tells the user about its own problems as well, since only
    /// the user can fix those. Not so about problems of the query: it was written by whoever asked
    /// for this page, and so is a better one.
    /// </remarks>
    public IReadOnlyList<RetrievalGap> Gaps { get; init; } = [];
}