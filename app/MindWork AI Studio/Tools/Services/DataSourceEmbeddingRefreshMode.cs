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

    /// <summary>
    /// The interval came round at which a data source is looked at again, since nothing reports its
    /// changes, e.g. a mailbox on a server.
    /// </summary>
    INTERVAL_CHECK,

    /// <summary>
    /// A tool is about to read what may have changed since the last run, e.g. the drafts of a
    /// mailbox. Unlike a retry of the user, it never signs in despite a refused sign-in.
    /// </summary>
    TOOL_REQUEST,
}