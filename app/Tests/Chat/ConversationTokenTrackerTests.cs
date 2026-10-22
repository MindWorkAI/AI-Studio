using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks when the token count is recomputed and when it is not.
/// </summary>
/// <remarks>
/// This is the part which kept going wrong. The number used to be wired to the places which change
/// the conversation -- fifteen of them in the end -- and four review rounds each found another place
/// which had been forgotten. So it is no longer wired to anything: whoever suspects a change nudges,
/// and what is checked here is that the tracker turns those nudges into the right amount of work.
///
/// The waits are generous on purpose. What is asserted is the behaviour, not the clock, so every
/// interval here is far enough apart that a busy build machine cannot turn one into the other.
/// </remarks>
[TestFixture]
public sealed class ConversationTokenTrackerTests
{
    /// <summary>
    /// A heartbeat which never fires, for the tests which are about nudges alone.
    /// </summary>
    private static readonly TimeSpan NO_HEARTBEAT = Timeout.InfiniteTimeSpan;

    [Test]
    public async Task ManyNudgesInARowCostOneCount()
    {
        //
        // Loading a chat touches several things one after the other, and every one of them renders.
        // Counting once per render would measure the same conversation half a dozen times.
        //
        var runs = 0;
        var firstRun = new TaskCompletionSource();

        await using var tracker = new ConversationTokenTracker(_ =>
        {
            Interlocked.Increment(ref runs);
            firstRun.TrySetResult();
            return Task.CompletedTask;
        }, () => TimeSpan.FromSeconds(2), NO_HEARTBEAT);

        tracker.Start();
        for (var i = 0; i < 50; i++)
            tracker.Nudge();

        await firstRun.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200);

        Assert.That(runs, Is.EqualTo(1));
    }

    [Test]
    public async Task ANudgeArrivingDuringACountLeadsToExactlyOneMore()
    {
        //
        // Something changed while we were reading, so the answer we just worked out may already be
        // out of date -- but only one further count can be needed, however many nudges arrived.
        //
        var runs = 0;
        var firstRunStarted = new TaskCompletionSource();
        var releaseFirstRun = new TaskCompletionSource();

        await using var tracker = new ConversationTokenTracker(async _ =>
        {
            if (Interlocked.Increment(ref runs) is not 1)
                return;

            firstRunStarted.TrySetResult();
            await releaseFirstRun.Task;
        }, () => TimeSpan.FromMilliseconds(100), NO_HEARTBEAT);

        tracker.Start();
        tracker.Nudge();
        await firstRunStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        //
        // Nudged while the first run is held up, five times over, because a render storm is what
        // this has to survive.
        //
        for (var i = 0; i < 5; i++)
            tracker.Nudge();

        releaseFirstRun.SetResult();
        await Task.Delay(1_000);

        Assert.That(runs, Is.EqualTo(2));
    }

    [Test]
    public async Task WithoutAnyNudgeTheHeartbeatStillCounts()
    {
        //
        // For what happens outside AI Studio: an attached file somebody edits in another program
        // changes what the next message costs, and nothing here renders because of it.
        //
        var runs = 0;

        await using var tracker = new ConversationTokenTracker(_ =>
        {
            Interlocked.Increment(ref runs);
            return Task.CompletedTask;
        }, () => TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200));

        tracker.Start();
        await Task.Delay(1_000);

        Assert.That(runs, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task TheQuietTimeIsAskedAnewAfterEveryRun()
    {
        //
        // Because the right answer changes with what is going on. Showing a new number renders, and
        // a render nudges, so while something moves continuously this delay is the entire cadence
        // -- and a chat which is waiting for a model wants a slower one than a chat which is not.
        //
        var runs = 0;
        var asked = 0;

        await using var tracker = new ConversationTokenTracker(_ =>
        {
            Interlocked.Increment(ref runs);
            return Task.CompletedTask;
        }, () =>
        {
            Interlocked.Increment(ref asked);
            return TimeSpan.FromMilliseconds(50);
        }, TimeSpan.FromMilliseconds(100));

        tracker.Start();
        await Task.Delay(1_000);

        Assert.Multiple(() =>
        {
            Assert.That(runs, Is.GreaterThanOrEqualTo(2));
            Assert.That(asked, Is.EqualTo(runs));
        });
    }

    [Test]
    public async Task NothingIsCountedAfterTheTrackerIsGone()
    {
        var runs = 0;
        var tracker = new ConversationTokenTracker(_ =>
        {
            Interlocked.Increment(ref runs);
            return Task.CompletedTask;
        }, () => TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));

        tracker.Start();
        await Task.Delay(400);
        await tracker.DisposeAsync();

        var afterDisposal = runs;
        await Task.Delay(400);

        Assert.Multiple(() =>
        {
            Assert.That(afterDisposal, Is.GreaterThan(0), "The tracker never ran, so this proves nothing about stopping it.");
            Assert.That(runs, Is.EqualTo(afterDisposal));
        });
    }

    [Test]
    public async Task ACountWhichHangsDoesNotHoldUpDisposal()
    {
        //
        // Counting ends in an IPC call to the runtime, and a component going away must not wait for
        // one which is not coming back. The token handed to the work is the way out, and this is
        // the test that it really is one.
        //
        var running = new TaskCompletionSource();
        var tracker = new ConversationTokenTracker(async token =>
        {
            running.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, () => TimeSpan.FromMilliseconds(50), NO_HEARTBEAT);

        tracker.Start();
        tracker.Nudge();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var disposal = tracker.DisposeAsync().AsTask();
        var finishedInTime = await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(2))) == disposal;

        Assert.That(finishedInTime, Is.True);
    }

    [Test]
    public async Task AFailedCountDoesNotEndTheTracker()
    {
        //
        // A tracker which died on one bad answer would leave a stale number standing forever, which
        // is the one failure this whole mechanism exists to rule out.
        //
        var runs = 0;

        await using var tracker = new ConversationTokenTracker(_ =>
        {
            Interlocked.Increment(ref runs);
            throw new InvalidOperationException("The tokenizer did not answer.");
        }, () => TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));

        tracker.Start();
        await Task.Delay(1_000);

        Assert.That(runs, Is.GreaterThanOrEqualTo(2));
    }
}