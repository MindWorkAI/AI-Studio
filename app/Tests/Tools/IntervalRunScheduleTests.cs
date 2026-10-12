using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when the runs asked for at an interval are due, by the clock, the way mailboxes are synced.
/// </summary>
[TestFixture]
public sealed class IntervalRunScheduleTests
{
    private static readonly TimeSpan INTERVAL = TimeSpan.FromMinutes(16);

    private static readonly TimeSpan FIRST_ROUND_DELAY = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan CHECK_PERIOD = TimeSpan.FromMinutes(1);

    private static readonly DateTimeOffset START = new(2026, 10, 2, 20, 17, 0, TimeSpan.Zero);

    private const string WORK = "4f2a6c1e-8b3d-4e5f-9a7c-1d2e3f4a5b6c";

    private const string PRIVATE = "7c9e1a3b-5d2f-4a6e-8b0c-2e4f6a8c0d1e";

    [Test]
    public void TheFirstRoundComesAfterTheFirstRoundDelay()
    {
        var schedule = StartSchedule();
        var tooEarly = schedule.TakeDue(START + TimeSpan.FromSeconds(30));
        var firstRound = schedule.TakeDue(START + FIRST_ROUND_DELAY);

        Assert.Multiple(() =>
        {
            Assert.That(tooEarly.IsRound, Is.False);
            Assert.That(tooEarly.DataSourceIds, Is.Empty);
            Assert.That(firstRound.IsRound, Is.True);
            Assert.That(firstRound.DataSourceIds, Is.EquivalentTo(new[] { WORK, PRIVATE }));
        });
    }

    [Test]
    public void ACheckWhichComesALittleEarlyByTheClockStillStartsTheRound()
    {
        var schedule = StartSchedule();
        Assert.That(schedule.TakeDue(START + FIRST_ROUND_DELAY - TimeSpan.FromMilliseconds(5)).IsRound, Is.True);
    }

    [Test]
    public void TheNextRoundComesAWholeIntervalLater()
    {
        var schedule = StartSchedule();
        var firstRound = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(firstRound);

        var rounds = Enumerable.Range(1, 16).Where(minutes => schedule.TakeDue(firstRound + TimeSpan.FromMinutes(minutes)).IsRound).ToList();
        Assert.That(rounds, Is.EqualTo(new[] { 16 }));
    }

    [Test]
    public void ARoundIsDueRightAfterWakingUp()
    {
        var schedule = StartSchedule();
        var firstRound = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(firstRound);
        schedule.TakeDue(firstRound + TimeSpan.FromMinutes(1));

        //
        // A timer which counts only the time awake would still wait for the rest of the interval.
        //
        Assert.That(schedule.TakeDue(firstRound + TimeSpan.FromHours(12)).IsRound, Is.True);
    }

    [Test]
    public void AClockSetBackPutsTheNextRoundOffByOneIntervalAtMost()
    {
        var schedule = StartSchedule();
        var firstRound = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(firstRound);

        var setBack = firstRound - TimeSpan.FromHours(1);
        var rightAfterSettingBack = schedule.TakeDue(setBack).IsRound;
        var oneIntervalLater = schedule.TakeDue(setBack + INTERVAL).IsRound;

        Assert.Multiple(() =>
        {
            Assert.That(rightAfterSettingBack, Is.False);
            Assert.That(oneIntervalLater, Is.True);
        });
    }

    [Test]
    public void EditingTheDataSourcesDoesNotMoveTheRounds()
    {
        var schedule = StartSchedule();
        var firstRound = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(firstRound);

        schedule.Track([PRIVATE], firstRound + TimeSpan.FromMinutes(5));
        var rightAfterEditing = schedule.TakeDue(firstRound + TimeSpan.FromMinutes(6));
        var nextRound = schedule.TakeDue(firstRound + INTERVAL);

        Assert.Multiple(() =>
        {
            Assert.That(rightAfterEditing.IsRound, Is.False);
            Assert.That(nextRound.IsRound, Is.True);
            Assert.That(nextRound.DataSourceIds, Is.EqualTo(new[] { PRIVATE }), "A mailbox which is no longer tracked is still synced.");
        });
    }

    [Test]
    public void TrackingAfterAPauseWaitsOnlyForTheFirstRoundDelay()
    {
        var schedule = StartSchedule();
        schedule.TakeDue(START + FIRST_ROUND_DELAY);
        schedule.StopAll();

        var restart = START + TimeSpan.FromMinutes(5);
        schedule.Track([WORK], restart);
        Assert.That(schedule.TakeDue(restart + FIRST_ROUND_DELAY).IsRound, Is.True);
    }

    [Test]
    public void AServerOutOfReachIsTriedAgainAfterGrowingPauses()
    {
        var schedule = StartSchedule();
        var now = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(now);

        var delays = new List<TimeSpan?>();
        for (var attempt = 0; attempt <= IntervalRunSchedule.RETRY_DELAYS.Length; attempt++)
            delays.Add(schedule.RecordServerOutOfReach(WORK, now));

        Assert.That(delays, Is.EqualTo(new TimeSpan?[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8), null }), "Once the retries are used up, only the rounds try again.");
    }

    [Test]
    public void ARetryRunsOnlyTheDataSourceWhoseServerWasOutOfReach()
    {
        var schedule = StartSchedule();
        var now = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(now);
        schedule.RecordServerOutOfReach(WORK, now);

        var tooEarly = schedule.TakeDue(now + TimeSpan.FromSeconds(30));
        var retry = schedule.TakeDue(now + TimeSpan.FromMinutes(1));
        var afterTheRetry = schedule.TakeDue(now + TimeSpan.FromMinutes(2));

        Assert.Multiple(() =>
        {
            Assert.That(tooEarly.DataSourceIds, Is.Empty);
            Assert.That(retry.IsRound, Is.False);
            Assert.That(retry.DataSourceIds, Is.EqualTo(new[] { WORK }));
            Assert.That(afterTheRetry.DataSourceIds, Is.Empty, "A retry ran twice.");
        });
    }

    [Test]
    public void ARoundTakesOverAPendingRetry()
    {
        var schedule = StartSchedule();
        var firstRound = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(firstRound);

        var lastFailure = firstRound + TimeSpan.FromMinutes(10);
        UseUpRetries(schedule, lastFailure);

        var nextRound = schedule.TakeDue(firstRound + INTERVAL);
        var whenTheRetryWasDue = schedule.TakeDue(lastFailure + IntervalRunSchedule.RETRY_DELAYS[^1]);

        Assert.Multiple(() =>
        {
            Assert.That(nextRound.IsRound, Is.True);
            Assert.That(whenTheRetryWasDue.DataSourceIds, Is.Empty, "The round ran the data source already.");
        });
    }

    [Test]
    public void ReachingTheServerStartsTheRetriesAnew()
    {
        var schedule = StartSchedule();
        var now = START + FIRST_ROUND_DELAY;
        UseUpRetries(schedule, now);

        schedule.RecordServerReached(WORK);
        Assert.That(schedule.RecordServerOutOfReach(WORK, now), Is.EqualTo(IntervalRunSchedule.RETRY_DELAYS[0]));
    }

    [Test]
    public void WakingUpStartsTheRetriesAnew()
    {
        var schedule = StartSchedule();
        var evening = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(evening);
        UseUpRetries(schedule, evening);

        //
        // The VPN tunnel was down all evening, and it comes up a little while after waking up.
        //
        var morning = evening + TimeSpan.FromHours(12);
        var roundAfterWakingUp = schedule.TakeDue(morning).IsRound;
        var retryDelay = schedule.RecordServerOutOfReach(WORK, morning);

        Assert.Multiple(() =>
        {
            Assert.That(roundAfterWakingUp, Is.True);
            Assert.That(retryDelay, Is.EqualTo(IntervalRunSchedule.RETRY_DELAYS[0]));
        });
    }

    [Test]
    public void ChecksAtTheCheckPeriodDoNotStartTheRetriesAnew()
    {
        var schedule = StartSchedule();
        var now = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(now);
        UseUpRetries(schedule, now);

        for (var minutes = 1; minutes <= 30; minutes++)
            schedule.TakeDue(now + TimeSpan.FromMinutes(minutes));

        Assert.That(schedule.RecordServerOutOfReach(WORK, now + TimeSpan.FromMinutes(30)), Is.Null, "A computer which is offline for hours would keep trying every few minutes.");
    }

    [Test]
    public void ADataSourceWhichIsNotTrackedIsNeverRetried()
    {
        var schedule = StartSchedule();
        var now = START + FIRST_ROUND_DELAY;
        schedule.TakeDue(now);
        schedule.RecordServerOutOfReach(WORK, now);
        schedule.Stop(WORK);

        var unknownDelay = schedule.RecordServerOutOfReach("0d6b8e2a-4c1f-4b3e-9d7a-5e2c8f1a3b6d", now);
        var due = schedule.TakeDue(now + TimeSpan.FromMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(unknownDelay, Is.Null);
            Assert.That(due.DataSourceIds, Is.Empty, "A deleted mailbox is still synced.");
        });
    }

    private static IntervalRunSchedule StartSchedule()
    {
        var schedule = new IntervalRunSchedule(INTERVAL, FIRST_ROUND_DELAY, CHECK_PERIOD);
        schedule.Track([WORK, PRIVATE], START);
        return schedule;
    }

    private static void UseUpRetries(IntervalRunSchedule schedule, DateTimeOffset now)
    {
        for (var attempt = 0; attempt < IntervalRunSchedule.RETRY_DELAYS.Length; attempt++)
            schedule.RecordServerOutOfReach(WORK, now);
    }
}