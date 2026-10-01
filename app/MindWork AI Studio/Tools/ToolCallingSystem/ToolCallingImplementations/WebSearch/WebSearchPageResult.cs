using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// One search hit as the tool returns it, with its page if that could be read.
/// </summary>
/// <remarks>
/// The two states belong to one type because everything after the retrieval treats them alike:
/// they are merged by URL, ranked together, filtered for prompt injections in the same request,
/// and numbered into one list of results. Only the outcome tells them apart, and it is the one
/// place that does: a retrieved page always comes with its content, and every other outcome
/// comes without one.
/// </remarks>
internal sealed class WebSearchPageResult(SearchCandidate candidate, RetrievedWebPage? retrievedPage, WebSearchPageRetrievalOutcome outcome)
{
    public SearchCandidate Candidate { get; } = candidate;

    public RetrievedWebPage? RetrievedPage { get; } = retrievedPage;

    public WebSearchPageRetrievalOutcome Outcome { get; } = outcome;

    public string ReturnedMarkdown { get; set; } = string.Empty;

    public bool ContentTruncated { get; set; }

    /// <summary>
    /// Whether this hit carries the page's own content rather than the search service's snippet.
    /// </summary>
    public bool HasPageContent => this.RetrievedPage is not null;

    /// <summary>
    /// The URL this hit stands for, which is the one a model may cite.
    /// </summary>
    /// <remarks>
    /// A page that was read is cited by where it was actually found, after every redirect. A hit
    /// without a page has no such address — nobody arrived anywhere — so the URL the search
    /// service reported has to do.
    /// </remarks>
    public Uri CitationUrl => this.RetrievedPage?.Page.FinalUrl ?? this.Candidate.RetrievalUrl;
}