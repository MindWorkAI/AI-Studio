namespace AIStudio.Tools.Services;

/// <summary>
/// The pauses of the indexing, e.g., while a backup copies the indexes.
/// </summary>
/// <remarks>
/// Pauses may overlap, and the indexing goes on once the last one ended. Every run which a pause
/// stopped is noted together with why it was asked for, so that it can be queued again for the same
/// reason once the indexing goes on: a retry the user asked for still signs in to a mailbox again
/// after its server refused a sign-in, cf. DataSourceRunQueue.
/// </remarks>
internal sealed class IndexingPauses
{
    private readonly Lock stateLock = new();

    /// <summary>
    /// The runs which a pause stopped, each with why it was asked for.
    /// </summary>
    private readonly Dictionary<string, DataSourceEmbeddingRefreshMode> stoppedRuns = new(StringComparer.OrdinalIgnoreCase);

    private int count;

    /// <summary>
    /// Completes once the last pause ended. Null while the indexing is not paused.
    /// </summary>
    private TaskCompletionSource? lastPauseEnded;

    /// <summary>
    /// Begins a pause. From now on, every run which is about to start waits.
    /// </summary>
    public void Begin()
    {
        lock (this.stateLock)
        {
            if (this.count++ == 0)
                this.lastPauseEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Notes a run which a pause stopped.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <param name="refreshMode">Why the run was asked for.</param>
    public void RecordStoppedRun(string dataSourceId, DataSourceEmbeddingRefreshMode refreshMode)
    {
        lock (this.stateLock)
            this.stoppedRuns.TryAdd(dataSourceId, refreshMode);
    }

    /// <summary>
    /// Ends a pause.
    /// </summary>
    /// <returns>The runs which the pauses stopped, once the last pause ended, so the caller queues them again. Otherwise, none.</returns>
    public IReadOnlyList<DataSourceRunRequest> End()
    {
        lock (this.stateLock)
        {
            if (this.count == 0 || --this.count > 0)
                return [];

            this.lastPauseEnded?.TrySetResult();
            this.lastPauseEnded = null;

            var stoppedRuns = this.stoppedRuns.Select(stoppedRun => new DataSourceRunRequest(stoppedRun.Key, stoppedRun.Value)).ToList();
            this.stoppedRuns.Clear();
            return stoppedRuns;
        }
    }

    /// <summary>
    /// Waits while the indexing is paused.
    /// </summary>
    /// <param name="token">Stops the waiting, e.g., because the run waiting here was stopped.</param>
    public Task WaitWhilePausedAsync(CancellationToken token)
    {
        lock (this.stateLock)
            return this.lastPauseEnded?.Task.WaitAsync(token) ?? Task.CompletedTask;
    }
}