using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which runs the queue of the indexing runs hands out, and why each of them happens.
/// </summary>
/// <remarks>
/// File watchers, intervals and clicks ask for runs, often many at once for the same data source. A
/// data source which runs twice embeds everything twice, at a cloud embedding provider for real
/// money. One which never runs again goes out of date without anybody noticing. A run in the wrong
/// mode is the subtle case: only a manual retry signs in to a mailbox again after its server refused
/// a sign-in.
/// </remarks>
[TestFixture]
public sealed class DataSourceRunQueueTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";

    private const string OTHER_DATA_SOURCE_ID = "5a1e7c3d-9b2f-4d8e-a6c4-3f0b9d2e1c75";

    [Test]
    public async Task RequestsForAWaitingDataSourceMergeIntoOneRun()
    {
        var queue = new DataSourceRunQueue();

        var first = await Request(queue, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK);
        var second = await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(DataSourceQueueRequestResult.QUEUED));
            Assert.That(second, Is.EqualTo(DataSourceQueueRequestResult.ALREADY_QUEUED));
            Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK) }), "The first request decides why the run happens.");
        });
    }

    [Test]
    public async Task RequestsDuringARunLeaveExactlyOneFollowUpRun()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);
        Assert.That(queue.TryDequeue(out _), Is.True);

        var first = await Request(queue, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK);
        var second = await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION);
        var requeue = queue.TryComplete(DATA_SOURCE_ID, true, out var pendingRefreshMode);
        if (requeue)
            await queue.EnqueueAsync(DATA_SOURCE_ID);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(DataSourceQueueRequestResult.RUNNING_MARKED_PENDING));
            Assert.That(second, Is.EqualTo(DataSourceQueueRequestResult.RUNNING));
            Assert.That(requeue, Is.True);
            Assert.That(pendingRefreshMode, Is.EqualTo(DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK), "The first request decides why the follow-up run happens.");
            Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK) }), "The running run may have read the data source before the changes, so one more run is needed, and one is enough.");
        });
    }

    [Test]
    public async Task ARunWithoutRequestsLeavesNoFollowUpRun()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);
        queue.TryDequeue(out _);

        var requeue = queue.TryComplete(DATA_SOURCE_ID, true, out _);
        var nextRequest = queue.TryReserve(DATA_SOURCE_ID, true, DataSourceEmbeddingRefreshMode.HASH_CHECK);

        Assert.Multiple(() =>
        {
            Assert.That(requeue, Is.False);
            Assert.That(nextRequest, Is.EqualTo(DataSourceQueueRequestResult.QUEUED), "The data source must not count as running after its run completed.");
        });
    }

    [Test]
    public async Task AFollowUpRunOfADataSourceWhichIsGoneIsDropped()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);
        queue.TryDequeue(out _);
        await Request(queue, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK);

        var requeue = queue.TryComplete(DATA_SOURCE_ID, false, out _);
        var nextRequest = queue.TryReserve(DATA_SOURCE_ID, true, DataSourceEmbeddingRefreshMode.HASH_CHECK);

        Assert.Multiple(() =>
        {
            Assert.That(requeue, Is.False);
            Assert.That(nextRequest, Is.EqualTo(DataSourceQueueRequestResult.QUEUED), "Neither the run nor its follow-up run may linger.");
        });
    }

    [Test]
    public async Task ADataSourceClearedWhileItWaitedLeavesNoRunBehind()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);

        queue.Clear(DATA_SOURCE_ID);

        Assert.That(queue.TryDequeue(out _), Is.False, "The data source was deleted while it waited. Its id is still in the channel, yet it must not run.");
    }

    [Test]
    public async Task ARepairedDataSourceRunsOnceAndInTheModeOfTheRepair()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);

        // What the repair does: forget the waiting run, then ask for a manual retry.
        queue.Clear(DATA_SOURCE_ID);
        await Request(queue, DataSourceEmbeddingRefreshMode.MANUAL_RETRY);

        Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.MANUAL_RETRY) }), "The id is in the channel twice now. Running both copies would embed the data source twice, and a run in the old mode would not sign in to a mailbox again.");
    }

    [Test]
    public async Task AManualRetryReplacesTheModeOfAWaitingRun()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.INTERVAL_CHECK);

        var retry = await Request(queue, DataSourceEmbeddingRefreshMode.MANUAL_RETRY);
        var later = await Request(queue, DataSourceEmbeddingRefreshMode.TOOL_REQUEST);

        Assert.Multiple(() =>
        {
            Assert.That(retry, Is.EqualTo(DataSourceQueueRequestResult.ALREADY_QUEUED));
            Assert.That(later, Is.EqualTo(DataSourceQueueRequestResult.ALREADY_QUEUED));
            Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.MANUAL_RETRY) }), "Only a manual retry signs in to a mailbox again after its server refused a sign-in, and no later request may take that back.");
        });
    }

    [Test]
    public async Task AManualRetryReplacesTheModeOfAFollowUpRun()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.INTERVAL_CHECK);
        queue.TryDequeue(out _);

        await Request(queue, DataSourceEmbeddingRefreshMode.INTERVAL_CHECK);
        await Request(queue, DataSourceEmbeddingRefreshMode.MANUAL_RETRY);
        await Request(queue, DataSourceEmbeddingRefreshMode.TOOL_REQUEST);
        queue.TryComplete(DATA_SOURCE_ID, true, out var pendingRefreshMode);

        Assert.That(pendingRefreshMode, Is.EqualTo(DataSourceEmbeddingRefreshMode.MANUAL_RETRY), "Only a manual retry signs in to a mailbox again after its server refused a sign-in, and no later request may take that back.");
    }

    [Test]
    public async Task ARunOfTheStartupCheckGetsExactlyOneFollowUpRun()
    {
        var queue = new DataSourceRunQueue();
        var begun = queue.TryBeginRun(DATA_SOURCE_ID);

        var first = await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION);
        var second = await Request(queue, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK);
        var requeue = queue.TryComplete(DATA_SOURCE_ID, true, out var pendingRefreshMode);
        if (requeue)
            await queue.EnqueueAsync(DATA_SOURCE_ID);

        Assert.Multiple(() =>
        {
            Assert.That(begun, Is.True);
            Assert.That(first, Is.EqualTo(DataSourceQueueRequestResult.RUNNING_MARKED_PENDING), "The run began outside the queue, yet it runs all the same.");
            Assert.That(second, Is.EqualTo(DataSourceQueueRequestResult.RUNNING));
            Assert.That(requeue, Is.True);
            Assert.That(pendingRefreshMode, Is.EqualTo(DataSourceEmbeddingRefreshMode.CONTINUATION));
            Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.CONTINUATION) }), "A data source which did its share of the work during the startup check must carry on afterward, exactly once.");
        });
    }

    [Test]
    public async Task TheStartupCheckLeavesOutADataSourceWhichRunsOrWaits()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);
        var running = queue.TryBeginRun(OTHER_DATA_SOURCE_ID);

        Assert.Multiple(() =>
        {
            Assert.That(running, Is.True);
            Assert.That(queue.TryBeginRun(DATA_SOURCE_ID), Is.False, "The data source waits in the queue, so it would run twice in a row.");
            Assert.That(queue.TryBeginRun(OTHER_DATA_SOURCE_ID), Is.False, "The data source runs already, so it would run twice at once.");
        });
    }

    [Test]
    public async Task WaitingForTheNextRunSkipsWhatWasCleared()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);
        queue.Clear(DATA_SOURCE_ID);

        var nextRun = queue.DequeueAsync(CancellationToken.None).AsTask();
        var waitedForTheNextRun = !nextRun.IsCompleted;
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK, OTHER_DATA_SOURCE_ID);
        var run = await nextRun.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(waitedForTheNextRun, Is.True, "Nothing was left to run.");
            Assert.That(run, Is.EqualTo(new DataSourceRunRequest(OTHER_DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.HASH_CHECK)));
        });
    }

    /// <summary>
    /// Asks for a run the way DataSourceEmbeddingService does: reserve a place, and enqueue the data source when it got one.
    /// </summary>
    private static async Task<DataSourceQueueRequestResult> Request(DataSourceRunQueue queue, DataSourceEmbeddingRefreshMode refreshMode, string dataSourceId = DATA_SOURCE_ID)
    {
        var result = queue.TryReserve(dataSourceId, true, refreshMode);
        if (result is DataSourceQueueRequestResult.QUEUED)
            await queue.EnqueueAsync(dataSourceId);

        return result;
    }

    private static List<DataSourceRunRequest> DequeueAll(DataSourceRunQueue queue)
    {
        var runs = new List<DataSourceRunRequest>();
        while (queue.TryDequeue(out var runRequest))
            runs.Add(runRequest);

        return runs;
    }
}