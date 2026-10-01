namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;

/// <summary>
/// One web hit of a Staan search.
/// </summary>
/// <remarks>
/// Staan also reports a shortened URL for display, the hostname, a favicon, and sometimes a
/// thumbnail. All of it serves presenting a hit in a result list, while this tool loads and
/// reads the page itself. Staan reports no publication date.
/// </remarks>
internal sealed record StaanSearchResult
{
    public string Title { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Snippet { get; init; } = string.Empty;
}