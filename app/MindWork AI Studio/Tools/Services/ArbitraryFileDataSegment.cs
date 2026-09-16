namespace AIStudio.Tools.Services;

/// <summary>
/// One piece of an extracted file, as the runtime delivered it.
/// </summary>
/// <param name="Content">The extracted text.</param>
/// <param name="TokenCount">The number of tokens of that text.</param>
/// <param name="PageNumber">The page that text came from, or null when it has none. Presentations and spreadsheets have none.</param>
public sealed record ArbitraryFileDataSegment(string Content, int TokenCount, int? PageNumber);
