namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    /// <summary>
    /// Pauses the indexing of every data source, e.g., while a backup copies the indexes.
    /// </summary>
    /// <remarks>
    /// Every run stops, and the pause waits until they have, since a run may be about to write to
    /// both stores. A document a run embedded completely stays stored, and its fingerprint keeps the
    /// next run from embedding it once more. Every run asked for during the pause waits for its end,
    /// the next sync of a mailbox among them. Ending the pause queues every data source whose run it
    /// stopped, for the same reason that run was asked for.<br/><br/>
    ///
    /// Pauses may overlap. The indexing goes on once the last one ended.
    /// </remarks>
    /// <param name="token">Stops waiting for the runs to stop, which ends the pause as well.</param>
    /// <returns>The pause. Disposing it ends the pause.</returns>
    public async Task<IAsyncDisposable> PauseIndexingAsync(CancellationToken token)
    {
        //
        // The pause begins before any run is stopped. A run which starts meanwhile is either among
        // the active runs below, or it finds the indexing paused and waits:
        //
        this.pauses.Begin();
        var pause = new IndexingPause(this);

        var stoppedRuns = new List<Task>();
        foreach (var (dataSourceId, activeRun) in this.activeRuns)
        {
            if (!TryCancelRun(activeRun))
                continue;

            this.pauses.RecordStoppedRun(dataSourceId, activeRun.RefreshMode);
            stoppedRuns.Add(activeRun.Completion.Task);
        }

        try
        {
            await Task.WhenAll(stoppedRuns).WaitAsync(token);
        }
        catch
        {
            await pause.DisposeAsync();
            throw;
        }

        logger.LogInformation("Paused the indexing of all data sources. Stopped {StoppedRuns} active runs.", stoppedRuns.Count);
        return pause;
    }

    /// <summary>
    /// Ends a pause, and once the last one ended, queues every data source whose run a pause stopped.
    /// </summary>
    private async Task EndPauseAsync()
    {
        var stoppedRuns = this.pauses.End();
        foreach (var stoppedRun in stoppedRuns)
        {
            // Deleted during the pause, so there is nothing to carry on with:
            if (!this.TryGetConfiguredIndexedSource(stoppedRun.DataSourceId, out var dataSource))
                continue;

            await this.QueueDataSourceAsync(dataSource, true, stoppedRun.RefreshMode);
        }
    }

    /// <summary>
    /// One pause, which ends once it is disposed.
    /// </summary>
    private sealed class IndexingPause(DataSourceEmbeddingService service) : IAsyncDisposable
    {
        private int ended;

        /// <summary>
        /// Ends the pause. Ending it again does nothing.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this.ended, 1) == 1)
                return;

            await service.EndPauseAsync();
        }
    }
}