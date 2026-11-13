namespace AIStudio.Tools.Services;

/// <summary>
/// The lanes of the queue of indexing runs, cf. DataSourceRunQueue.
/// </summary>
/// <remarks>
/// Every run in the front lane comes out before any run in the back lane. DataSourceEmbeddingService
/// picks the lane when it enqueues a run: a data source which carries on a pass of its indexing waits
/// in the back lane, cf. IndexingPass. A mailbox therefore waits for one step at most, rather than for
/// hours behind a large folder, and large data sources take turns with each other.
/// </remarks>
internal enum DataSourceQueueLane
{
    /// <summary>
    /// For a data source which begins a pass, e.g. a mailbox at its interval.
    /// </summary>
    FRONT,

    /// <summary>
    /// For a data source which carries on a pass, after it did its share of the work.
    /// </summary>
    BACK,
}