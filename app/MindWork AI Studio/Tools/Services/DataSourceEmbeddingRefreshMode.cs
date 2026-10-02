namespace AIStudio.Tools.Services;

/// <summary>
/// Why an indexing run of a data source was started.
/// </summary>
internal enum DataSourceEmbeddingRefreshMode
{
    STARTUP_HASH_CHECK,
    HASH_CHECK,
    WATCHER_HASH_CHECK,
    MANUAL_RETRY,

    /// <summary>
    /// Carries on where the last run of the same data source stopped after its share of the work.
    /// </summary>
    CONTINUATION,
}