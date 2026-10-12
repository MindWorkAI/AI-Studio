namespace AIStudio.Tools.Services;

/// <remarks>
/// A watcher here is whatever an indexer uses to notice that one of its data sources changed, a file
/// system watcher for local files, an interval for mailboxes. The indexers own them; this service
/// only decides when they run.
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

        var supportedSources = this.GetConfiguredIndexedSources();
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
    /// Queues the run an indexer asked for after it noticed a change, or because its interval came round.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source which changed.</param>
    /// <param name="refreshMode">Why the indexer asks.</param>
    private async Task RequestRunAsync(string dataSourceId, DataSourceEmbeddingRefreshMode refreshMode)
    {
        if (!this.TryGetConfiguredIndexedSource(dataSourceId, out var dataSource))
            return;

        if (this.statuses.TryGetValue(dataSourceId, out var status) && IsWaitingForSignIn(status))
        {
            logger.LogDebug("Skipped the requested run of data source '{DataSourceId}' because its sign-in failed and waits for the user. RefreshMode={RefreshMode}.", dataSourceId, refreshMode);
            return;
        }

        await this.QueueDataSourceAsync(dataSource, true, refreshMode);
    }

    /// <summary>
    /// Whether a data source waits for the user since its sign-in failed.
    /// </summary>
    /// <remarks>
    /// Then only the user starts its next run, by saving a new password or with a retry. The indexer
    /// would not sign in on any other run either, but every one of them would make the row of the
    /// data source change from failed to queued and back, every interval anew.
    /// </remarks>
    /// <param name="status">The current status of the data source.</param>
    internal static bool IsWaitingForSignIn(DataSourceEmbeddingStatus status) => status.Attention is DataSourceAttention.AUTH_FAILED;
}
