namespace AIStudio.Tools.Services;

/// <summary>
/// What became of a request to queue an indexing run, cf. DataSourceRunQueue.TryReserve.
/// </summary>
internal enum DataSourceQueueRequestResult
{
    /// <summary>
    /// The data source has its place in the queue reserved, and whoever asked enqueues it now.
    /// </summary>
    QUEUED,

    /// <summary>
    /// The data source waits in the queue already, and the request merged into that run.
    /// </summary>
    ALREADY_QUEUED,

    /// <summary>
    /// The data source runs, and either has its follow-up run already or was asked for none.
    /// </summary>
    RUNNING,

    /// <summary>
    /// The data source runs, and gets one follow-up run once the current one completes.
    /// </summary>
    RUNNING_MARKED_PENDING,
}