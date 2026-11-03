namespace AIStudio.Tools.Services;

/// <summary>
/// One piece of an extracted file, as the runtime delivered it.
/// </summary>
/// <param name="Content">The extracted text.</param>
/// <param name="TokenCount">The number of tokens of that text.</param>
/// <param name="Location">Where that text came from: its page, its sheet and row, or its line. Presentations have none.</param>
public sealed record ArbitraryFileDataSegment(string Content, int TokenCount, SourceLocation Location);
