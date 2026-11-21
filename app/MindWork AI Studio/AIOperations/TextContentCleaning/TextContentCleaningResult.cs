namespace AIStudio.AIOperations.TextContentCleaning;

/// <summary>
/// The text of a web page after the cleaning.
/// </summary>
/// <param name="Markdown">The cleaned page, or the page as it came when it could not be cleaned.</param>
/// <param name="IsCleaned">Whether a model cleaned the page.</param>
public sealed record TextContentCleaningResult(string Markdown, bool IsCleaned);