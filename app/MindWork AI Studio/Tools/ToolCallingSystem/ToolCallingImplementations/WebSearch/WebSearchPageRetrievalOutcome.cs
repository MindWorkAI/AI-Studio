namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// What became of one search hit's page.
/// </summary>
/// <remarks>
/// A hit whose page could not be read is still reported, with the search service's snippet in
/// place of the content, and then this says why there is no content. That is worth a value of
/// its own per hit rather than only a counter for the whole search: a page blocked by the
/// network safety checks will stay unreachable, while one that timed out may well answer
/// later, and only the model deciding what to do next can act on the difference.
/// </remarks>
internal enum WebSearchPageRetrievalOutcome
{
    RETRIEVED,
    BLOCKED,
    PAGE_TIMED_OUT,
    RETRIEVAL_TIMED_OUT,
    FAILED,
    NO_READABLE_CONTENT,
}