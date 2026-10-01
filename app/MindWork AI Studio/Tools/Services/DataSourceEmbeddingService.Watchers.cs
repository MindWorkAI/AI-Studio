using AIStudio.Settings;

namespace AIStudio.Tools.Services;

/// <remarks>
/// A watcher here is whatever an indexer uses to notice that one of its data sources changed, a file
/// system watcher for local files. The indexers own them; this service only decides when they run.
/// </remarks>
public sealed partial class DataSourceEmbeddingService
{
    /// <summary>
    /// Lets every indexer track changes to its data sources, or stops all tracking.
    /// </summary>
    /// <remarks>
    /// Tracking runs only while local data sources refresh on their own, and not before the startup
    /// hash check is done.
    /// </remarks>
    private void RefreshWatchers()
    {
        if (!settingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh)
        {
            this.RemoveAllWatchers();
            return;
        }

        if (Volatile.Read(ref this.startupHashCheckCompleted) == 0)
        {
            logger.LogDebug("File watchers are not activated yet because the startup persisted hash check has not completed.");
            this.RemoveAllWatchers();
            return;
        }

        var supportedSources = settingsManager.ConfigurationData.DataSources
            .Where(this.IsSupportedIndexedSource)
            .OfType<IIndexedDataSource>()
            .ToList();

        foreach (var indexer in this.indexers)
            indexer.TrackChanges(supportedSources.Where(indexer.Supports).ToList(), this.RequestRunAsync);
    }

    private void RemoveWatcher(string dataSourceId)
    {
        foreach (var indexer in this.indexers)
            indexer.StopTracking(dataSourceId);
    }

    private void RemoveAllWatchers()
    {
        foreach (var indexer in this.indexers)
            indexer.StopTrackingAll();
    }

    private void DisposeWatchers()
    {
        foreach (var indexer in this.indexers)
            indexer.Dispose();
    }

    /// <summary>
    /// Queues the run an indexer asked for after it noticed a change.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source which changed.</param>
    /// <param name="refreshMode">Why the indexer asks.</param>
    private async Task RequestRunAsync(string dataSourceId, DataSourceEmbeddingRefreshMode refreshMode)
    {
        if (this.TryGetConfiguredIndexedSource(dataSourceId, out var dataSource))
            await this.QueueDataSourceAsync(dataSource, true, refreshMode);
    }
}
