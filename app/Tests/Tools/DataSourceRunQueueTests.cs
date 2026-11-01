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
/// a sign-in. The order counts as well: a mailbox which waits behind a large folder misses its new
/// mails for hours.
/// </remarks>
[TestFixture]
public sealed class DataSourceRunQueueTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";

    private const string OTHER_DATA_SOURCE_ID = "5a1e7c3d-9b2f-4d8e-a6c4-3f0b9d2e1c75";

    private const string MAILBOX_ID = "0c8e2b4f-3d7a-4e19-b5c6-8a2f1d9e7b30";

    [Test]
    public async Task RequestsForAWaitingDataSourceMergeIntoOneRun()
    {
        var queue = new DataSourceRunQueue();

        var first = await Request(queue, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK);
        var second = await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(DataSourceQueueRequestResult.QUEUED));
            Assert.That(second, Is.EqualTo(DataSourceQueueRequestResult.ALREADY_QUEUED));
            Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK) }), "The first request decides why the run happens.");
        }
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
            await queue.EnqueueAsync(DATA_SOURCE_ID, DataSourceQueueLane.FRONT);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(DataSourceQueueRequestResult.RUNNING_MARKED_PENDING));
            Assert.That(second, Is.EqualTo(DataSourceQueueRequestResult.RUNNING));
            Assert.That(requeue, Is.True);
            Assert.That(pendingRefreshMode, Is.EqualTo(DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK), "The first request decides why the follow-up run happens.");
            Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK) }), "The running run may have read the data source before the changes, so one more run is needed, and one is enough.");
        }
    }

    [Test]
    public async Task ARunWithoutRequestsLeavesNoFollowUpRun()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);
        queue.TryDequeue(out _);

        var requeue = queue.TryComplete(DATA_SOURCE_ID, true, out _);
        var nextRequest = queue.TryReserve(DATA_SOURCE_ID, true, DataSourceEmbeddingRefreshMode.HASH_CHECK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requeue, Is.False);
            Assert.That(nextRequest, Is.EqualTo(DataSourceQueueRequestResult.QUEUED), "The data source must not count as running after its run completed.");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requeue, Is.False);
            Assert.That(nextRequest, Is.EqualTo(DataSourceQueueRequestResult.QUEUED), "Neither the run nor its follow-up run may linger.");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(retry, Is.EqualTo(DataSourceQueueRequestResult.ALREADY_QUEUED));
            Assert.That(later, Is.EqualTo(DataSourceQueueRequestResult.ALREADY_QUEUED));
            Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.MANUAL_RETRY) }), "Only a manual retry signs in to a mailbox again after its server refused a sign-in, and no later request may take that back.");
        }
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
            await queue.EnqueueAsync(DATA_SOURCE_ID, DataSourceQueueLane.BACK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(begun, Is.True);
            Assert.That(first, Is.EqualTo(DataSourceQueueRequestResult.RUNNING_MARKED_PENDING), "The run began outside the queue, yet it runs all the same.");
            Assert.That(second, Is.EqualTo(DataSourceQueueRequestResult.RUNNING));
            Assert.That(requeue, Is.True);
            Assert.That(pendingRefreshMode, Is.EqualTo(DataSourceEmbeddingRefreshMode.CONTINUATION));
            Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.CONTINUATION) }), "A data source which did its share of the work during the startup check must carry on afterward, exactly once.");
        }
    }

    [Test]
    public async Task TheStartupCheckLeavesOutADataSourceWhichRunsOrWaits()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.HASH_CHECK);
        var running = queue.TryBeginRun(OTHER_DATA_SOURCE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(running, Is.True);
            Assert.That(queue.TryBeginRun(DATA_SOURCE_ID), Is.False, "The data source waits in the queue, so it would run twice in a row.");
            Assert.That(queue.TryBeginRun(OTHER_DATA_SOURCE_ID), Is.False, "The data source runs already, so it would run twice at once.");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(waitedForTheNextRun, Is.True, "Nothing was left to run.");
            Assert.That(run, Is.EqualTo(new DataSourceRunRequest(OTHER_DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.HASH_CHECK)));
        }
    }

    [Test]
    public async Task AMailboxWaitsForOneStepAtMost()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION, DATA_SOURCE_ID, DataSourceQueueLane.BACK);
        await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION, OTHER_DATA_SOURCE_ID, DataSourceQueueLane.BACK);
        Assert.That(queue.TryDequeue(out var step), Is.True);

        // The interval of the mailbox comes due while a large folder does its step:
        await Request(queue, DataSourceEmbeddingRefreshMode.INTERVAL_CHECK, MAILBOX_ID);
        await CarryOn(queue, step!.DataSourceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(step.DataSourceId, Is.EqualTo(DATA_SOURCE_ID));
            Assert.That(DequeueAll(queue).Select(run => run.DataSourceId), Is.EqualTo(new[] { MAILBOX_ID, OTHER_DATA_SOURCE_ID, DATA_SOURCE_ID }), "The mailbox must not wait behind every step of every large data source, which would add up to hours.");
        }
    }

    [Test]
    public async Task DataSourcesWhichCarryOnTakeTurns()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION, DATA_SOURCE_ID, DataSourceQueueLane.BACK);
        await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION, OTHER_DATA_SOURCE_ID, DataSourceQueueLane.BACK);

        var steps = new List<string>();
        for (var step = 0; step < 4; step++)
        {
            Assert.That(queue.TryDequeue(out var runRequest), Is.True);
            steps.Add(runRequest!.DataSourceId);
            await CarryOn(queue, runRequest.DataSourceId);
        }

        Assert.That(steps, Is.EqualTo(new[] { DATA_SOURCE_ID, OTHER_DATA_SOURCE_ID, DATA_SOURCE_ID, OTHER_DATA_SOURCE_ID }), "A data source which takes hours must not hold up another one which takes hours as well.");
    }

    [Test]
    public async Task ADataSourceRepairedWhileItCarriedOnRunsOnceAndAtTheFront()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION, DATA_SOURCE_ID, DataSourceQueueLane.BACK);
        await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION, OTHER_DATA_SOURCE_ID, DataSourceQueueLane.BACK);

        // What the repair does: forget the waiting run, which also drops the pass, then ask for a manual retry.
        queue.Clear(DATA_SOURCE_ID);
        await Request(queue, DataSourceEmbeddingRefreshMode.MANUAL_RETRY);

        Assert.That(DequeueAll(queue), Is.EqualTo(new[] { new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.MANUAL_RETRY), new DataSourceRunRequest(OTHER_DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.CONTINUATION) }), "The copy left behind in the back lane must not run the data source a second time.");
    }

    [Test]
    public async Task ACopyLeftBehindInTheFrontLaneLetsNoLaterRunOvertake()
    {
        var queue = new DataSourceRunQueue();
        await Request(queue, DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK);
        await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION, OTHER_DATA_SOURCE_ID, DataSourceQueueLane.BACK);

        // The repair of a data source which waits in the front lane leaves a copy of its id behind there:
        queue.Clear(DATA_SOURCE_ID);
        await Request(queue, DataSourceEmbeddingRefreshMode.MANUAL_RETRY);
        Assert.That(queue.TryDequeue(out var repair), Is.True);
        await CarryOn(queue, repair!.DataSourceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(repair, Is.EqualTo(new DataSourceRunRequest(DATA_SOURCE_ID, DataSourceEmbeddingRefreshMode.MANUAL_RETRY)));
            Assert.That(DequeueAll(queue).Select(run => run.DataSourceId), Is.EqualTo(new[] { OTHER_DATA_SOURCE_ID, DATA_SOURCE_ID }), "The next step of the repaired data source carries on its pass, so it waits its turn in the back lane.");
        }
    }

    /// <summary>
    /// Asks for a run the way DataSourceEmbeddingService does: reserve a place, and enqueue the data source when it got one.
    /// </summary>
    private static async Task<DataSourceQueueRequestResult> Request(DataSourceRunQueue queue, DataSourceEmbeddingRefreshMode refreshMode, string dataSourceId = DATA_SOURCE_ID, DataSourceQueueLane lane = DataSourceQueueLane.FRONT)
    {
        var result = queue.TryReserve(dataSourceId, true, refreshMode);
        if (result is DataSourceQueueRequestResult.QUEUED)
            await queue.EnqueueAsync(dataSourceId, lane);

        return result;
    }

    /// <summary>
    /// Ends the step of a running data source the way DataSourceEmbeddingService does when the step left more to do.
    /// </summary>
    private static async Task CarryOn(DataSourceRunQueue queue, string dataSourceId)
    {
        await Request(queue, DataSourceEmbeddingRefreshMode.CONTINUATION, dataSourceId);
        if (queue.TryComplete(dataSourceId, true, out _))
            await queue.EnqueueAsync(dataSourceId, DataSourceQueueLane.BACK);
    }

    private static List<DataSourceRunRequest> DequeueAll(DataSourceRunQueue queue)
    {
        var runs = new List<DataSourceRunRequest>();
        while (queue.TryDequeue(out var runRequest))
            runs.Add(runRequest);

        return runs;
    }
}