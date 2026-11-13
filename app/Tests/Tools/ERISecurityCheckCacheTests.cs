using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how long the answer of an ERI server to the check of its security requirements is used.
/// </summary>
/// <remarks>
/// One message checks the ERI data sources several times, and every check signs in. A server
/// which limits sign-ins would start refusing, so an answer is kept for a minute. A server which
/// did not answer is asked again sooner, and a check which is still running is joined.
/// </remarks>
[TestFixture]
public sealed class ERISecurityCheckCacheTests
{
    private static readonly DataSourceERI_V1 INTRANET = new()
    {
        Id = "33333333-3333-3333-3333-333333333333",
        Name = "Intranet",
        Type = DataSourceType.ERI_V1,
        Hostname = "https://intranet.example.com",
        Port = 443,
    };

    private static readonly DataSourceERI_V1 WIKI = INTRANET with
    {
        Id = "44444444-4444-4444-4444-444444444444",
        Name = "Wiki",
    };

    [Test]
    public void ARunningCheckIsJoined()
    {
        var running = new TaskCompletionSource<ERISecurityCheckResult>();

        Assert.That(ERISecurityCheckCache.IsValid(running.Task, TimeSpan.FromMinutes(5)), Is.True, "Starting a second check would sign in a second time.");
    }

    [Test]
    public void AnAnswerIsUsedForOneMinute()
    {
        var answered = Answer(true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ERISecurityCheckCache.IsValid(answered, TimeSpan.FromSeconds(59)), Is.True, "The rounds of one answer share a single sign-in.");
            Assert.That(ERISecurityCheckCache.IsValid(answered, TimeSpan.FromSeconds(61)), Is.False, "The operator may change the requirements at any time.");
        }
    }

    [Test]
    public void AServerWithoutAnswerIsAskedAgainSooner()
    {
        var unanswered = Answer(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ERISecurityCheckCache.IsValid(unanswered, TimeSpan.FromSeconds(14)), Is.True, "The rounds of one answer do not wait six seconds each for the same server.");
            Assert.That(ERISecurityCheckCache.IsValid(unanswered, TimeSpan.FromSeconds(16)), Is.False, "The server may well be back.");
        }
    }

    [Test]
    public void AFailedCheckIsNeverUsed()
    {
        var failed = Task.FromException<ERISecurityCheckResult>(new InvalidOperationException());

        Assert.That(ERISecurityCheckCache.IsValid(failed, TimeSpan.Zero), Is.False);
    }

    [Test]
    public void ACheckStillRunningIsSharedByEveryoneWhoAsks()
    {
        var running = new TaskCompletionSource<ERISecurityCheckResult>();
        var fetches = 0;
        var cache = new ERISecurityCheckCache(_ => { fetches++; return running.Task; }, new ManualTime());

        var first = cache.GetAsync(INTRANET);
        var second = cache.GetAsync(INTRANET);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fetches, Is.EqualTo(1), "Semantic Search checks when it is offered and again before every search; one sign-in has to do for both.");
            Assert.That(second, Is.SameAs(first));
        }
    }

    [Test]
    public void AnExpiredAnswerAsksTheServerAgain()
    {
        var time = new ManualTime();
        var fetches = 0;
        var cache = new ERISecurityCheckCache(_ => { fetches++; return Answer(true); }, time);

        cache.GetAsync(INTRANET);
        time.Advance(TimeSpan.FromSeconds(30));
        cache.GetAsync(INTRANET);
        Assert.That(fetches, Is.EqualTo(1), "Within a minute, the answer still holds.");

        time.Advance(TimeSpan.FromSeconds(31));
        cache.GetAsync(INTRANET);
        Assert.That(fetches, Is.EqualTo(2), "The operator may have changed the requirements meanwhile.");
    }

    [Test]
    public void AChangedConfigurationAsksTheServerRightAway()
    {
        var fetches = 0;
        var cache = new ERISecurityCheckCache(_ => { fetches++; return Answer(true); }, new ManualTime());

        cache.GetAsync(INTRANET);
        cache.GetAsync(INTRANET with { Hostname = "https://elsewhere.example.com" });

        Assert.That(fetches, Is.EqualTo(2), "Another server may require something else, although the data source kept its ID.");
    }

    [Test]
    public void EachDataSourceIsCheckedOnItsOwn()
    {
        var asked = new List<string>();
        var cache = new ERISecurityCheckCache(source => { asked.Add(source.Id); return Answer(true); }, new ManualTime());

        cache.GetAsync(INTRANET);
        cache.GetAsync(WIKI);
        cache.GetAsync(INTRANET);

        Assert.That(asked, Is.EqualTo(new[] { INTRANET.Id, WIKI.Id }));
    }

    [Test]
    public void ExpiredChecksAreDroppedWhenAnotherOneStarts()
    {
        var time = new ManualTime();
        var cache = new ERISecurityCheckCache(_ => Answer(true), time);

        cache.GetAsync(INTRANET);
        time.Advance(TimeSpan.FromMinutes(2));
        cache.GetAsync(WIKI);

        Assert.That(cache.Count, Is.EqualTo(1), "A deleted data source must not stay in the cache for as long as the app runs.");
    }

    private static Task<ERISecurityCheckResult> Answer(bool isReachable) => Task.FromResult(isReachable ? new ERISecurityCheckResult(true, null) : ERISecurityCheckResult.UNREACHABLE);

    /// <summary>
    /// A clock which only moves when a test moves it.
    /// </summary>
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan duration) => this.now += duration;

        public override DateTimeOffset GetUtcNow() => this.now;
    }
}