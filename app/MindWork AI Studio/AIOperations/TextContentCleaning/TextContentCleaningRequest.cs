namespace AIStudio.AIOperations.TextContentCleaning;

/// <summary>
/// A web page to clean.
/// </summary>
/// <param name="Markdown">The page as Markdown, already filtered for prompt injections.</param>
/// <param name="SourceUrl">The address of the page, for turning relative links into absolute ones.</param>
public sealed record TextContentCleaningRequest(string Markdown, string SourceUrl);
