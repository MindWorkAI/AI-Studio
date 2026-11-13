namespace AIStudio.Tools.Services;

/// <summary>
/// Files outside the configured patterns, summarized by extension or by a known extensionless
/// file type so a large folder does not put every deliberately excluded path into memory or the
/// user interface.
/// </summary>
/// <param name="FileType">The normalized extension including its leading dot, a known extensionless filename, or an empty string for other files without an extension.</param>
/// <param name="FileCount">How many files of this summarized type were found.</param>
public sealed record DataSourceEmbeddingFilePatternGroup(string FileType, int FileCount);