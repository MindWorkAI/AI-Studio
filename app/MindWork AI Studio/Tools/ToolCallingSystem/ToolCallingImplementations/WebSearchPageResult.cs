using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

internal sealed class WebSearchPageResult(SearchCandidate candidate, RetrievedWebPage retrievedPage)
{
    public SearchCandidate Candidate { get; } = candidate;

    public RetrievedWebPage RetrievedPage { get; } = retrievedPage;

    public string ReturnedMarkdown { get; set; } = string.Empty;

    public bool ContentTruncated { get; set; }
}