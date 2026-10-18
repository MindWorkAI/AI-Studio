namespace AIStudio.Tools.Services;

/// <summary>
/// A file which matches the configured part of a local data source, but was skipped before text
/// extraction.
/// </summary>
/// <remarks>
/// Files below folders the user chose not to scan never reach enumeration and therefore do not
/// appear here. Generated transcript artifacts and temporary/system files are implementation
/// details and stay hidden as well.
/// </remarks>
/// <param name="FilePath">The absolute path of the ignored file.</param>
/// <param name="Reason">Why the file was ignored.</param>
public sealed record DataSourceEmbeddingIgnoredFile(string FilePath, DataSourceEmbeddingIgnoreReason Reason);