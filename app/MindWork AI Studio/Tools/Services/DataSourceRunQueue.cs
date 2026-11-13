using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace AIStudio.Tools.Services;

/// <summary>
/// The queue of indexing runs: which data sources wait for a run, which run, and which get one more run afterward.
/// </summary>
/// <remarks>
/// A data source holds at most one place in the queue, and at most one follow-up run while it runs.
///
/// The queue has two lanes, cf. DataSourceQueueLane. Every run in the front lane comes out before
/// any run in the back lane, and each lane hands out its runs in the order they came in. The data
/// sources in the back lane therefore take turns.
///
/// The channel carries copies of ids, each with its lane and a number. Why a run was asked for is
/// kept next to it, together with the number of the copy which counts, and taking a run out of the
/// queue takes both along. Any other copy of the same id which is still in the channel then finds
/// nothing, or another number, and is skipped. That is how a data source which was deleted or
/// repaired while it waited leaves nothing behind: no run in an outdated mode, no second run
/// circling through the queue next to the one which was queued anew, and no copy in the front lane
/// which lets a later run of the data source overtake the back lane.
///
/// Everything but the channel is guarded by one lock, so reserving, starting and completing a run
/// never see each other halfway.
/// </remarks>
internal sealed class DataSourceRunQueue
{
    private readonly Channel<QueuedCopy> channel = Channel.CreateUnboundedPrioritized(new UnboundedPrioritizedChannelOptions<QueuedCopy> { Comparer = Comparer<QueuedCopy>.Create(CompareCopies) });

    /// <summary>
    /// The data sources which wait for a run, each with why it was asked for.
    /// </summary>
    private readonly Dictionary<string, DataSourceEmbeddingRefreshMode> queuedModes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The data sources which wait in the channel, each with the number of the copy which counts.
    /// </summary>
    /// <remarks>
    /// A data source whose place is reserved gets its number once it is enqueued.
    /// </remarks>
    private readonly Dictionary<string, long> queuedCopyNumbers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The data sources whose run has started and not completed yet.
    /// </summary>
    private readonly HashSet<string> runningIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The running data sources which get one more run once the current one completes, each with why it was asked for.
    /// </summary>
    private readonly Dictionary<string, DataSourceEmbeddingRefreshMode> pendingModes = new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock stateLock = new();

    private long lastCopyNumber;

    /// <summary>
    /// One copy of the id of a data source in the channel.
    /// </summary>
    /// <param name="DataSourceId">The id of the data source.</param>
    /// <param name="Lane">The lane the copy waits in.</param>
    /// <param name="Number">Counts up with every copy. It keeps each lane in the order its copies came in, and tells the copy which counts from those left behind.</param>
    private sealed record QueuedCopy(string DataSourceId, DataSourceQueueLane Lane, long Number);

    /// <summary>
    /// Reserves a place in the queue for a data source, or one follow-up run while it runs.
    /// </summary>
    /// <remarks>
    /// A request for a data source which holds a place or a follow-up run already merges into it.
    /// The first request decides why the run happens, and any later one only confirms that it is
    /// needed. A manual retry is the exception and replaces any other mode: it is the only one which
    /// signs in to a mailbox again after its server refused a sign-in, cf. MailboxIndexer. Behind a
    /// sync at the interval which waits already, the retry the user asked for would be lost.
    ///
    /// The place is taken once its id is in the channel, cf. EnqueueAsync.
    /// </remarks>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <param name="queueAfterCurrentRun">Whether a request while the data source runs leaves a follow-up run.</param>
    /// <param name="refreshMode">Why the run is asked for.</param>
    /// <returns>QUEUED when the caller has to enqueue the data source now, or what became of the request instead.</returns>
    public DataSourceQueueRequestResult TryReserve(string dataSourceId, bool queueAfterCurrentRun, DataSourceEmbeddingRefreshMode refreshMode)
    {
        lock (this.stateLock)
        {
            if (this.runningIds.Contains(dataSourceId))
            {
                if (!queueAfterCurrentRun)
                    return DataSourceQueueRequestResult.RUNNING;

                return TryAddOrMerge(this.pendingModes, dataSourceId, refreshMode)
                    ? DataSourceQueueRequestResult.RUNNING_MARKED_PENDING
                    : DataSourceQueueRequestResult.RUNNING;
            }

            return TryAddOrMerge(this.queuedModes, dataSourceId, refreshMode)
                ? DataSourceQueueRequestResult.QUEUED
                : DataSourceQueueRequestResult.ALREADY_QUEUED;
        }
    }

    /// <summary>
    /// Puts a data source into the queue, once its place is reserved.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <param name="lane">The lane the data source waits in.</param>
    /// <param name="token">The cancellation token.</param>
    public ValueTask EnqueueAsync(string dataSourceId, DataSourceQueueLane lane, CancellationToken token = default)
    {
        QueuedCopy copy;
        lock (this.stateLock)
        {
            // Cleared since its place was reserved, e.g. because the data source was deleted:
            if (!this.queuedModes.ContainsKey(dataSourceId))
                return ValueTask.CompletedTask;

            copy = new(dataSourceId, lane, ++this.lastCopyNumber);
            this.queuedCopyNumbers[dataSourceId] = copy.Number;
        }

        return this.channel.Writer.WriteAsync(copy, token);
    }

    /// <summary>
    /// Waits for the next run, takes it out of the queue, and marks it as running.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The run.</returns>
    public async ValueTask<DataSourceRunRequest> DequeueAsync(CancellationToken token)
    {
        while (true)
        {
            if (this.TryDequeue(out var runRequest))
                return runRequest;

            if (!await this.channel.Reader.WaitToReadAsync(token))
                throw new ChannelClosedException();
        }
    }

    /// <summary>
    /// Takes the next run out of the queue, when there is one, and marks it as running.
    /// </summary>
    /// <param name="runRequest">The run, when there is one.</param>
    /// <returns>True when a run was taken out of the queue.</returns>
    public bool TryDequeue([NotNullWhen(true)] out DataSourceRunRequest? runRequest)
    {
        while (this.channel.Reader.TryRead(out var copy))
        {
            lock (this.stateLock)
            {
                // Released or cleared while it waited, or enqueued anew since, with another copy which counts instead:
                if (!this.queuedCopyNumbers.TryGetValue(copy.DataSourceId, out var number) || number != copy.Number)
                    continue;

                this.queuedCopyNumbers.Remove(copy.DataSourceId);
                this.queuedModes.Remove(copy.DataSourceId, out var refreshMode);
                this.runningIds.Add(copy.DataSourceId);
                runRequest = new(copy.DataSourceId, refreshMode);
                return true;
            }
        }

        runRequest = null;
        return false;
    }

    /// <summary>
    /// Marks a data source as running which did not come out of the queue, unless it runs or waits in the queue already.
    /// </summary>
    /// <remarks>
    /// For the startup check, which works through every data source on its own. Its runs have to
    /// count as running as well: a request during one of them then leaves one follow-up run, instead
    /// of a second run next to it.
    /// </remarks>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <returns>True when the caller runs the data source now, and completes the run with TryComplete.</returns>
    public bool TryBeginRun(string dataSourceId)
    {
        lock (this.stateLock)
        {
            if (this.queuedModes.ContainsKey(dataSourceId))
                return false;

            return this.runningIds.Add(dataSourceId);
        }
    }

    /// <summary>
    /// Marks the run of a data source as completed, and reserves the place of its follow-up run when it has one.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <param name="requeuePending">False drops the follow-up run, e.g. because the data source is gone.</param>
    /// <param name="pendingRefreshMode">Why the follow-up run was asked for.</param>
    /// <returns>True when the caller has to enqueue the follow-up run now.</returns>
    public bool TryComplete(string dataSourceId, bool requeuePending, out DataSourceEmbeddingRefreshMode pendingRefreshMode)
    {
        lock (this.stateLock)
        {
            this.runningIds.Remove(dataSourceId);

            if (!this.pendingModes.Remove(dataSourceId, out pendingRefreshMode))
                return false;

            return requeuePending && TryAddOrMerge(this.queuedModes, dataSourceId, pendingRefreshMode);
        }
    }

    /// <summary>
    /// Gives up the reserved place of a data source, because it could not be enqueued.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    public void Release(string dataSourceId)
    {
        lock (this.stateLock)
        {
            this.queuedModes.Remove(dataSourceId);
            this.queuedCopyNumbers.Remove(dataSourceId);
        }
    }

    /// <summary>
    /// Forgets the place and the follow-up run of a data source, e.g. because it was deleted.
    /// </summary>
    /// <remarks>
    /// A run which is going on stays marked as running until it completes. Whoever calls this stops
    /// that run on their own.
    /// </remarks>
    /// <param name="dataSourceId">The id of the data source.</param>
    public void Clear(string dataSourceId)
    {
        lock (this.stateLock)
        {
            this.queuedModes.Remove(dataSourceId);
            this.queuedCopyNumbers.Remove(dataSourceId);
            this.pendingModes.Remove(dataSourceId);
        }
    }

    /// <summary>
    /// Orders the copies in the channel: the front lane before the back lane, and each lane in the order its copies came in.
    /// </summary>
    private static int CompareCopies(QueuedCopy x, QueuedCopy y)
    {
        if (x.Lane != y.Lane)
            return x.Lane is DataSourceQueueLane.FRONT ? -1 : 1;

        return x.Number.CompareTo(y.Number);
    }

    /// <summary>
    /// Records why a run of a data source was asked for, or merges the request into the one recorded already.
    /// </summary>
    /// <param name="modes">The recorded runs.</param>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <param name="refreshMode">Why the run is asked for.</param>
    /// <returns>True when there was no run recorded for the data source yet.</returns>
    private static bool TryAddOrMerge(Dictionary<string, DataSourceEmbeddingRefreshMode> modes, string dataSourceId, DataSourceEmbeddingRefreshMode refreshMode)
    {
        if (modes.TryAdd(dataSourceId, refreshMode))
            return true;

        if (refreshMode is DataSourceEmbeddingRefreshMode.MANUAL_RETRY)
            modes[dataSourceId] = refreshMode;

        return false;
    }
}