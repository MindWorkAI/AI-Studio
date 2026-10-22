using AIStudio.Settings;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Asks for a run of each tracked data source at a fixed interval, and soon again for one whose
/// server was out of reach.
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
/// index is not that much behind after AI Studio started. Whether a round or a retry is due goes by
/// the clock, cf. IntervalRunSchedule; the timer only asks once per check period. A timer alone
/// would count only the time the computer is awake.
/// </remarks>
/// <param name="interval">The time between two rounds.</param>
/// <param name="firstRoundDelay">The time from the start of the tracking to the first round.</param>
/// <param name="refreshMode">Why the runs are asked for, as the embedding service gets to know.</param>
/// <param name="logger">The logger of the embedding service.</param>
internal sealed class IntervalRunRequester(TimeSpan interval, TimeSpan firstRoundDelay, DataSourceEmbeddingRefreshMode refreshMode, ILogger logger) : IDisposable
{
    /// <summary>
    /// How often the timer asks what is due. After the computer woke up, a round comes no later than this.
    /// </summary>
    private static readonly TimeSpan CHECK_PERIOD = TimeSpan.FromMinutes(1);

    private readonly Lock stateLock = new();
    private readonly IntervalRunSchedule schedule = new(interval, firstRoundDelay, CHECK_PERIOD);
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
            this.schedule.Track(dataSources.Select(dataSource => dataSource.Id), DateTimeOffset.UtcNow);
            this.requestRun = requestRunCallback;

            if (!this.schedule.IsTrackingAny)
            {
                this.StopTimer();
                return;
            }

            //
            // The first check comes when the first round is due, and every further one a check
            // period later.
            //
            this.timer ??= new Timer(this.OnCheckDue, null, firstRoundDelay, CHECK_PERIOD);
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
            this.schedule.Stop(dataSourceId);
            if (!this.schedule.IsTrackingAny)
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
            this.schedule.StopAll();
            this.StopTimer();
        }
    }

    /// <summary>
    /// Notes that the server of a data source was out of reach, so it is tried again soon.
    /// </summary>
    /// <remarks>
    /// Only for a network which is down or a host out of reach. A server which answered, even with a
    /// refusal, is reached.
    /// </remarks>
    /// <param name="dataSourceId">The id of the data source.</param>
    public void RecordServerOutOfReach(string dataSourceId)
    {
        TimeSpan? delay;
        lock (this.stateLock)
            delay = this.schedule.RecordServerOutOfReach(dataSourceId, DateTimeOffset.UtcNow);

        if (delay is { } pause)
            logger.LogInformation("The server of data source '{DataSourceId}' was out of reach. It is tried again in {Minutes} minute(s).", dataSourceId, pause.TotalMinutes);
        else
            logger.LogDebug("The server of data source '{DataSourceId}' was out of reach. No earlier retry is planned for it.", dataSourceId);
    }

    /// <summary>
    /// Notes that the server of a data source could be reached.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    public void RecordServerReached(string dataSourceId)
    {
        lock (this.stateLock)
            this.schedule.RecordServerReached(dataSourceId);
    }

    /// <summary>
    /// Asks for a run of every tracked data source, which is what one round does.
    /// </summary>
    internal async Task RequestRunsAsync()
    {
        string[] ids;
        Func<string, DataSourceEmbeddingRefreshMode, Task>? callback;
        lock (this.stateLock)
        {
            ids = [..this.schedule.DataSourceIds];
            callback = this.requestRun;
        }

        await this.RequestAsync(ids, callback);
    }

    /// <summary>
    /// Asks for the runs which are due now: a round, or the retries of servers which were out of reach.
    /// </summary>
    private async Task RequestDueRunsAsync()
    {
        (bool IsRound, IReadOnlyList<string> DataSourceIds) due;
        Func<string, DataSourceEmbeddingRefreshMode, Task>? callback;
        lock (this.stateLock)
        {
            due = this.schedule.TakeDue(DateTimeOffset.UtcNow);
            callback = this.requestRun;
        }

        if (!due.IsRound)
            foreach (var id in due.DataSourceIds)
                logger.LogInformation("Asking again for a run of data source '{DataSourceId}', since its server was out of reach.", id);

        await this.RequestAsync(due.DataSourceIds, callback);
    }

    /// <summary>
    /// Asks for a run of each of the given data sources.
    /// </summary>
    /// <remarks>
    /// A request which fails costs only its own data source the run.
    /// </remarks>
    private async Task RequestAsync(IReadOnlyList<string> ids, Func<string, DataSourceEmbeddingRefreshMode, Task>? callback)
    {
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
    /// Asks for what is due when the timer elapses. RequestAsync catches every failure itself.
    /// </summary>
    private void OnCheckDue(object? state) => _ = this.RequestDueRunsAsync();

    private void StopTimer()
    {
        this.timer?.Dispose();
        this.timer = null;
    }

    #region Implementation of IDisposable

    public void Dispose() => this.StopAll();

    #endregion
}