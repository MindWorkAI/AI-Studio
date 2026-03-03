namespace AIStudio.Provider.Perplexity;

/// <summary>
/// Data model for a search result.
/// </summary>
/// <param name="Title">The title of the search result.</param>
/// <param name="URL">The URL of the search result.</param>
public sealed record SearchResult(string Title, string URL) : Source(Title, URL, SourceOrigin.LLM);