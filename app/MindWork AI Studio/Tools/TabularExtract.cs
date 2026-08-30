namespace AIStudio.Tools;

/// <summary>
/// The tabular data a message holds.
/// </summary>
/// <param name="Content">The data itself, without the surrounding Markdown code fence.</param>
/// <param name="Format">The format this data gets written as.</param>
public readonly record struct TabularExtract(string Content, FileExportFormat Format);