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
/// </remarks>
internal interface IIndexedSourceIndexer
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
}