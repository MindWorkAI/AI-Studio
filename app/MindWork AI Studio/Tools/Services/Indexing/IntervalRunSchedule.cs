namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Decides which data sources are due for a run: all of them once an interval came round, and one
/// whose server was out of reach a little earlier than that.
/// </summary>
/// <remarks>
/// Everything here goes by the clock of the system. A timer counts only the time the computer is
/// awake, so after a night asleep, the rest of an interval would still have to pass before the next
/// round. By the clock, that round is due as soon as the computer wakes up.
///
/// A server out of reach is tried again a few times with growing pauses, instead of only with the
/// next round. After waking up or logging in, a VPN tunnel is often up a minute or two later than
/// the network, and its mailbox would otherwise wait for a whole interval. The retries start anew
/// once the server could be reached, when the tracking starts, and after the computer was asleep,
/// so a server which was out of reach all evening is tried again soon the next morning. Once they
/// are used up, only the rounds try again: a computer which is offline for hours does not keep
/// trying every few minutes.
///
/// The owner asks what is due once per check period and passes the current time to every call,
/// which keeps this free of timers. It is not thread-safe; the owner locks.
/// </remarks>
/// <param name="interval">The time between two rounds.</param>
/// <param name="firstRoundDelay">The time from the start of the tracking to the first round.</param>
/// <param name="checkPeriod">How often the owner asks what is due. A gap of more than twice as long means the computer was asleep.</param>
internal sealed class IntervalRunSchedule(TimeSpan interval, TimeSpan firstRoundDelay, TimeSpan checkPeriod)
{
    /// <summary>
    /// The pauses before the retries after a server was out of reach, in this order.
    /// </summary>
    internal static readonly TimeSpan[] RETRY_DELAYS = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8)];

    /// <summary>
    /// How much earlier than planned a run counts as due.
    /// </summary>
    /// <remarks>
    /// The timer of the owner and the clock of the system are two different clocks. A check planned
    /// for the very moment a run is due may come a little early by the clock, and would otherwise put
    /// the run off by a whole check period.
    /// </remarks>
    private static readonly TimeSpan EARLINESS_TOLERANCE = TimeSpan.FromSeconds(1);

    private readonly HashSet<string> dataSourceIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RetryState> retries = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset nextRoundUtc;
    private DateTimeOffset lastCheckUtc;

    /// <summary>
    /// Whether any data source is tracked.
    /// </summary>
    public bool IsTrackingAny => this.dataSourceIds.Count > 0;

    /// <summary>
    /// The ids of the tracked data sources.
    /// </summary>
    public IReadOnlyCollection<string> DataSourceIds => this.dataSourceIds;

    /// <summary>
    /// Tracks exactly the given data sources from now on.
    /// </summary>
    /// <remarks>
    /// Which data sources are tracked never moves the rounds, so editing them cannot keep putting the
    /// rounds off. Only when nothing was tracked before does the first round come after the first
    /// round delay.
    /// </remarks>
    /// <param name="ids">The ids of the data sources.</param>
    /// <param name="now">The current time.</param>
    public void Track(IEnumerable<string> ids, DateTimeOffset now)
    {
        var wasTrackingAny = this.IsTrackingAny;
        this.dataSourceIds.Clear();
        this.dataSourceIds.UnionWith(ids);

        if (!wasTrackingAny)
        {
            this.nextRoundUtc = now + firstRoundDelay;
            this.lastCheckUtc = now;
            this.retries.Clear();
            return;
        }

        foreach (var id in this.retries.Keys.Where(id => !this.dataSourceIds.Contains(id)).ToList())
            this.retries.Remove(id);
    }

    /// <summary>
    /// Stops tracking one data source.
    /// </summary>
    /// <param name="id">The id of the data source.</param>
    public void Stop(string id)
    {
        this.dataSourceIds.Remove(id);
        this.retries.Remove(id);
    }

    /// <summary>
    /// Stops tracking any data source.
    /// </summary>
    public void StopAll()
    {
        this.dataSourceIds.Clear();
        this.retries.Clear();
    }

    /// <summary>
    /// Takes what is due now: a round of every tracked data source, or the retries whose pause is over.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>Whether a round is due, and the ids of the data sources to run.</returns>
    public (bool IsRound, IReadOnlyList<string> DataSourceIds) TakeDue(DateTimeOffset now)
    {
        //
        // The owner asks once per check period. A much longer gap means the computer was asleep,
        // and right after waking up, a server may be out of reach for a little while.
        //
        if (now - this.lastCheckUtc > checkPeriod * 2)
            this.ChangeEveryRetry(retry => retry with { Attempts = 0 });

        this.lastCheckUtc = now;

        //
        // A clock which was set back must not put the next round off by more than one interval.
        //
        if (this.nextRoundUtc - now > interval)
            this.nextRoundUtc = now + interval;

        if (IsDue(this.nextRoundUtc, now))
        {
            this.nextRoundUtc = now + interval;

            //
            // The round runs every data source anyway, a pending retry included.
            //
            this.ChangeEveryRetry(retry => retry with { DueUtc = null });
            return (true, [..this.dataSourceIds]);
        }

        var dueRetries = this.retries.Where(entry => entry.Value.DueUtc is { } dueUtc && IsDue(dueUtc, now)).Select(entry => entry.Key).ToList();
        foreach (var id in dueRetries)
            this.retries[id] = this.retries[id] with { DueUtc = null };

        return (false, dueRetries);
    }

    /// <summary>
    /// Notes that the server of a data source was out of reach, and plans the next retry.
    /// </summary>
    /// <param name="id">The id of the data source.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The pause before the retry, or null when the retries are used up or the data source is not tracked.</returns>
    public TimeSpan? RecordServerOutOfReach(string id, DateTimeOffset now)
    {
        if (!this.dataSourceIds.Contains(id))
            return null;

        //
        // A data source without an entry has no retries behind it, which is just what the default
        // value says.
        //
        var attempts = this.retries.GetValueOrDefault(id).Attempts;
        if (attempts >= RETRY_DELAYS.Length)
            return null;

        var delay = RETRY_DELAYS[attempts];
        this.retries[id] = new RetryState(attempts + 1, now + delay);
        return delay;
    }

    /// <summary>
    /// Notes that the server of a data source could be reached, so its next outage starts the retries anew.
    /// </summary>
    /// <param name="id">The id of the data source.</param>
    public void RecordServerReached(string id) => this.retries.Remove(id);

    private static bool IsDue(DateTimeOffset dueUtc, DateTimeOffset now) => now >= dueUtc - EARLINESS_TOLERANCE;

    /// <summary>
    /// Changes the retries of every data source.
    /// </summary>
    /// <remarks>
    /// Goes over a copy of the entries, since writing a changed value back would otherwise break the
    /// enumeration. There are as many entries as data sources whose server was out of reach.
    /// </remarks>
    /// <param name="change">How a retry changes.</param>
    private void ChangeEveryRetry(Func<RetryState, RetryState> change)
    {
        foreach (var (id, retry) in this.retries.ToList())
            this.retries[id] = change(retry);
    }

    /// <summary>
    /// The retries of one data source whose server was out of reach. The default value stands for none.
    /// </summary>
    /// <param name="Attempts">How many retries were planned since the series started.</param>
    /// <param name="DueUtc">When the next retry is due, or null when none is planned.</param>
    private readonly record struct RetryState(int Attempts, DateTimeOffset? DueUtc);
}