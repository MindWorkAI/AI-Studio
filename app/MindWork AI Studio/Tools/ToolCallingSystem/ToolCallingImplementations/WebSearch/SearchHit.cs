namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// One hit of a search service, in the form every backend can express.
/// </summary>
/// <remarks>
/// This is the smallest common denominator of the search APIs: everything else they report
/// about a hit is about presenting it, and this tool loads the page itself. Hits arrive in the
/// order the service ranked them; turning them into candidates is the candidate collector's
/// job. What a service does not report stays empty rather than null, because the tool reports
/// these fields either way.
/// </remarks>
/// <param name="Url">Where the hit points.</param>
/// <param name="Title">The title the service reports, which is not necessarily the page's own.</param>
/// <param name="Snippet">The excerpt the service reports.</param>
/// <param name="PublishedDate">When the page was published, as the service spells it, or empty when it does not say.</param>
internal sealed record SearchHit(string Url, string Title, string Snippet, string PublishedDate = "");