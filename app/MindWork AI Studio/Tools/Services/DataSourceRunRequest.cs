namespace AIStudio.Tools.Services;

/// <summary>
/// A run of a data source, as it comes out of the queue.
/// </summary>
/// <param name="DataSourceId">The id of the data source.</param>
/// <param name="RefreshMode">Why the run was asked for.</param>
internal sealed record DataSourceRunRequest(string DataSourceId, DataSourceEmbeddingRefreshMode RefreshMode);