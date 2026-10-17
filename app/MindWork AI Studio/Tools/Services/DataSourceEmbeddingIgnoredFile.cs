namespace AIStudio.Tools.Services;

/// <summary>
/// Why a file found while enumerating a local data source was deliberately left out of its index.
/// </summary>
public enum DataSourceEmbeddingIgnoreReason
{
    MEDIA_NOT_SUPPORTED,
    TOO_LARGE,
}

/// <summary>
/// A file which matches the configured part of a local data source, but was skipped before text
/// extraction.
/// </summary>
/// <remarks>
/// Files below folders the user chose not to scan never reach enumeration and therefore do not
/// appear here. Temporary and system files are implementation details and stay hidden as well.
/// </remarks>
/// <param name="FilePath">The absolute path of the ignored file.</param>
/// <param name="Reason">Why the file was ignored.</param>
public sealed record DataSourceEmbeddingIgnoredFile(string FilePath, DataSourceEmbeddingIgnoreReason Reason);

/// <summary>
/// Files outside the configured patterns, summarized by extension or by a known extensionless
/// file type so a large folder does not put every deliberately excluded path into memory or the
/// user interface.
/// </summary>
/// <param name="FileType">The normalized extension including its leading dot, a known extensionless filename, or an empty string for other files without an extension.</param>
/// <param name="FileCount">How many files of this summarized type were found.</param>
public sealed record DataSourceEmbeddingFilePatternGroup(string FileType, int FileCount);
