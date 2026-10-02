using AIStudio.Settings;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Asks for a run of each tracked data source at a fixed interval.
/// </summary>
/// <remarks>
/// For a kind of data source whose changes nothing reports, e.g. a mailbox on a server, so an indexer
/// has to look again from time to time. The indexer hands its tracking over to this, and the
/// embedding service decides as for every indexer whether anything is tracked at all.
///
/// One timer serves all data sources of an indexer, and they come round together. The queue works
/// through them one after the other anyway. Which data sources are tracked never moves the rounds,
/// so editing the data sources cannot keep putting them off. One added later joins the next round;
/// whoever adds it queues it right away regardless.
///
/// The first round comes shortly after tracking starts rather than a whole interval later, so the
/// index is not that much behind after AI Studio started.
/// </remarks>
/// <param name="interval">The time between two rounds.</param>
/// <param name="firstRoundDelay">The time from the start of the tracking to the first round.</param>
/// <param name="refreshMode">Why the runs are asked for, as the embedding service gets to know.</param>
/// <param name="logger">The logger of the embedding service.</param>
internal sealed class IntervalRunRequester(TimeSpan interval, TimeSpan firstRoundDelay, DataSourceEmbeddingRefreshMode refreshMode, ILogger logger) : IDisposable
{
    private readonly Lock stateLock = new();
    private readonly HashSet<string> dataSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private Func<string, DataSourceEmbeddingRefreshMode, Task>? requestRun;
    private Timer? timer;

    /// <summary>
    /// Tracks exactly the given data sources from now on.
    /// </summary>
    /// <param name="dataSources">The data sources to track.</param>
    /// <param name="requestRunCallback">Asks the embedding service for a run of the data source with the given id.</param>
    public void Track(IReadOnlyCollection<IIndexedDataSource> dataSources, Func<string, DataSourceEmbeddingRefreshMode, Task> requestRunCallback)
    {
        lock (this.stateLock)
        {
            this.dataSourceIds.Clear();
            this.dataSourceIds.UnionWith(dataSources.Select(dataSource => dataSource.Id));
            this.requestRun = requestRunCallback;

            if (this.dataSourceIds.Count is 0)
            {
                this.StopTimer();
                return;
            }

            this.timer ??= new Timer(this.OnRoundDue, null, firstRoundDelay, interval);
        }
    }

    /// <summary>
    /// Stops tracking one data source.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    public void Stop(string dataSourceId)
    {
        lock (this.stateLock)
        {
            this.dataSourceIds.Remove(dataSourceId);
            if (this.dataSourceIds.Count is 0)
                this.StopTimer();
        }
    }

    /// <summary>
    /// Stops tracking any data source.
    /// </summary>
    public void StopAll()
    {
        lock (this.stateLock)
        {
            this.dataSourceIds.Clear();
            this.StopTimer();
        }
    }

    /// <summary>
    /// Asks for a run of every tracked data source, which is what one round does.
    /// </summary>
    /// <remarks>
    /// A request which fails costs only its own data source the round.
    /// </remarks>
    internal async Task RequestRunsAsync()
    {
        string[] ids;
        Func<string, DataSourceEmbeddingRefreshMode, Task>? callback;
        lock (this.stateLock)
        {
            ids = [..this.dataSourceIds];
            callback = this.requestRun;
        }

        if (callback is null)
            return;

        foreach (var id in ids)
        {
            try
            {
                await callback(id, refreshMode);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not queue the data source '{DataSourceId}' for its regular run.", id);
            }
        }
    }

    /// <summary>
    /// Starts a round when the timer elapses. RequestRunsAsync catches every failure itself.
    /// </summary>
    private void OnRoundDue(object? state) => _ = this.RequestRunsAsync();

    private void StopTimer()
    {
        this.timer?.Dispose();
        this.timer = null;
    }

    #region Implementation of IDisposable

    public void Dispose() => this.StopAll();

    #endregion
}