using AIStudio.Settings;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Indexes one kind of data source: knows how to find its documents and how to read them.
/// </summary>
/// <remarks>
/// Everything else is the same for every kind and stays out of here: the embedding service queues
/// the runs, prepares each one in an IndexedRunContext and reports the progress, and the context
/// embeds and stores the documents. A new kind of data source needs nothing but an indexer of its
/// own, and tables of its own for whatever it has to remember beyond its documents.
///
/// What an indexer does bring is how it notices that a data source changed. Files are watched by
/// the file system; another kind may have to look again at an interval. Whether anything is tracked
/// at all is not the indexer's to decide: the embedding service starts and stops the tracking.
/// </remarks>
internal interface IIndexedSourceIndexer : IDisposable
{
    /// <summary>
    /// Whether this indexer reads the documents of a data source.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <returns>True when this indexer is the one for it.</returns>
    public bool Supports(IDataSourceBase dataSource);

    /// <summary>
    /// Works through the documents of the data source of a run, from finding them to completing the run.
    /// </summary>
    /// <param name="context">The prepared run, whose data source this indexer supports.</param>
    /// <param name="refreshMode">Why the run was started.</param>
    /// <param name="token">The cancellation token.</param>
    public Task ProcessAsync(IndexedRunContext context, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token);

    /// <summary>
    /// Keeps track of changes to the given data sources, and of nothing else.
    /// </summary>
    /// <remarks>
    /// Called whenever the configured data sources may have changed, always with all of those this
    /// indexer supports. A data source which was tracked before and is missing now is no longer
    /// tracked.
    /// </remarks>
    /// <param name="dataSources">The data sources to track.</param>
    /// <param name="requestRun">Asks the embedding service for a run of the data source with the given id.</param>
    public void TrackChanges(IReadOnlyCollection<IIndexedDataSource> dataSources, Func<string, DataSourceEmbeddingRefreshMode, Task> requestRun);

    /// <summary>
    /// Stops tracking changes to one data source, and drops what was about to be reported for it.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    public void StopTracking(string dataSourceId);

    /// <summary>
    /// Stops tracking changes to any data source.
    /// </summary>
    public void StopTrackingAll();
}