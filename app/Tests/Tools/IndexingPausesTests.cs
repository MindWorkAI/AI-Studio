using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the pauses of the indexing, which a backup takes while it copies the indexes.
/// </summary>
/// <remarks>
/// A run which wrote to the stores while they are copied would leave a backup whose two stores do
/// not fit together. Once the backup is done, though, every data source whose run the pause stopped
/// has to carry on, for the same reason it ran: a retry the user asked for is the only run which
/// signs in to a mailbox again after its server refused a sign-in.
/// </remarks>
[TestFixture]
public sealed class IndexingPausesTests
{
    [Test]
    public async Task ARunWaitsUntilThePauseEnded()
    {
        var pauses = new IndexingPauses();
        pauses.Begin();

        var waiting = pauses.WaitWhilePausedAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.That(waiting.IsCompleted, Is.False, "The backup still copies the indexes.");

        pauses.End();
        await waiting;
    }

    [Test]
    public void WithoutAPauseARunStartsRightAway()
    {
        var pauses = new IndexingPauses();

        Assert.That(pauses.WaitWhilePausedAsync(CancellationToken.None).IsCompleted, Is.True);
    }

    [Test]
    public async Task TheIndexingGoesOnOnceTheLastPauseEnded()
    {
        var pauses = new IndexingPauses();
        pauses.Begin();
        pauses.Begin();
        pauses.RecordStoppedRun("mailbox", DataSourceEmbeddingRefreshMode.MANUAL_RETRY);

        var stoppedRunsAfterTheFirstEnd = pauses.End();
        var waiting = pauses.WaitWhilePausedAsync(CancellationToken.None);
        await Task.Delay(50);

        Assert.Multiple(() =>
        {
            Assert.That(stoppedRunsAfterTheFirstEnd, Is.Empty, "The other pause still holds the indexing back, so nothing may start yet.");
            Assert.That(waiting.IsCompleted, Is.False);
        });

        var stoppedRuns = pauses.End();
        await waiting;

        Assert.That(stoppedRuns, Is.EqualTo(new[] { new DataSourceRunRequest("mailbox", DataSourceEmbeddingRefreshMode.MANUAL_RETRY) }), "The retry the user asked for stays a retry.");
    }

    [Test]
    public void AStoppedRunIsQueuedAgainOnlyOnce()
    {
        var pauses = new IndexingPauses();
        pauses.Begin();
        pauses.RecordStoppedRun("folder", DataSourceEmbeddingRefreshMode.WATCHER_HASH_CHECK);
        pauses.RecordStoppedRun("folder", DataSourceEmbeddingRefreshMode.HASH_CHECK);

        Assert.Multiple(() =>
        {
            Assert.That(pauses.End(), Has.Count.EqualTo(1));
            Assert.That(pauses.End(), Is.Empty, "Ending a pause which does not exist changes nothing.");
        });
    }

    [Test]
    public void AStoppedRunStopsWaiting()
    {
        var pauses = new IndexingPauses();
        pauses.Begin();
        using var stopRun = new CancellationTokenSource();

        var waiting = pauses.WaitWhilePausedAsync(stopRun.Token);
        stopRun.Cancel();

        Assert.That(async () => await waiting, Throws.InstanceOf<OperationCanceledException>(), "Deleting a data source ends the wait of its run, as it does in front of the gate.");
    }
}