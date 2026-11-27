using AIStudio.Settings.DataModel;
using AIStudio.Tools.Pauses;

namespace AIStudio.Tests.Tools.Pauses;

/// <summary>
/// Checks which bounds of a random pause apply, as the user set them, as configuration plugins lock them, and within the limits of each feature.
/// </summary>
[TestFixture]
public sealed class PauseRangeTests
{
    private static readonly Func<bool> LOCKED = () => true;

    private static readonly Func<bool> UNLOCKED = () => false;

    private static readonly PauseLimits INDEXING = DataDataSourceIndexing.PAUSE_LIMITS;

    private static readonly PauseLimits BATCH_PROCESSING = DataBatchProcessing.PAUSE_LIMITS;

    [Test]
    public void BoundsInOrderStayAsTheyAre()
    {
        Assert.That(PauseRange.GetEffective(INDEXING, 10, UNLOCKED, 20, UNLOCKED), Is.EqualTo(new PauseRange(10, 20)));
    }

    [Test]
    public void AnUpperBoundBelowTheLowerOneIsRaisedToIt()
    {
        Assert.That(PauseRange.GetEffective(INDEXING, 60, UNLOCKED, 20, UNLOCKED), Is.EqualTo(new PauseRange(60, 60)));
    }

    [Test]
    public void ALockedLowerBoundRaisesTheUpperOne()
    {
        Assert.That(PauseRange.GetEffective(INDEXING, 60, LOCKED, 20, UNLOCKED), Is.EqualTo(new PauseRange(60, 60)));
    }

    [Test]
    public void ALockedUpperBoundLowersTheLowerOne()
    {
        Assert.That(PauseRange.GetEffective(INDEXING, 60, UNLOCKED, 20, LOCKED), Is.EqualTo(new PauseRange(20, 20)));
    }

    [Test]
    public void BothBoundsLockedTheWrongWayRoundLetTheLongerPauseWin()
    {
        Assert.That(PauseRange.GetEffective(INDEXING, 60, LOCKED, 20, LOCKED), Is.EqualTo(new PauseRange(60, 60)));
    }

    [Test]
    public void BoundsOutsideTheLimitsAreClamped()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PauseRange.GetEffective(INDEXING, -5, UNLOCKED, 1_000, UNLOCKED), Is.EqualTo(new PauseRange(0, 300)));
            Assert.That(PauseRange.GetEffective(BATCH_PROCESSING, -5, UNLOCKED, 1_000, UNLOCKED), Is.EqualTo(new PauseRange(6, 300)));
        }
    }

    [Test]
    public void BoundsBelowTheShortestPauseOfAFeatureAreRaisedToIt()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PauseRange.GetEffective(BATCH_PROCESSING, 0, UNLOCKED, 3, UNLOCKED), Is.EqualTo(new PauseRange(6, 6)));
            Assert.That(PauseRange.GetEffective(BATCH_PROCESSING, 2, UNLOCKED, 20, UNLOCKED), Is.EqualTo(new PauseRange(6, 20)));
        }
    }

    [Test]
    public void ALockedUpperBoundLowersTheLowerOneOnlyToTheShortestPauseOfAFeature()
    {
        Assert.That(PauseRange.GetEffective(BATCH_PROCESSING, 60, UNLOCKED, 2, LOCKED), Is.EqualTo(new PauseRange(6, 6)));
    }

    [Test]
    public void TheLocksAreAskedOnlyWhileTheBoundsAreTheWrongWayRound()
    {
        var asked = 0;
        var countingLock = () =>
        {
            asked++;
            return false;
        };

        PauseRange.GetEffective(INDEXING, 10, countingLock, 10, countingLock);
        PauseRange.GetEffective(INDEXING, 10, countingLock, 20, countingLock);
        PauseRange.GetEffective(BATCH_PROCESSING, 0, countingLock, 3, countingLock);
        Assert.That(asked, Is.Zero);

        PauseRange.GetEffective(INDEXING, 20, countingLock, 10, countingLock);
        Assert.That(asked, Is.Positive);
    }

    [Test]
    public void TheIndexingTakesItsBoundsFromItsSettings()
    {
        var settings = new DataDataSourceIndexing
        {
            MinimumPauseBetweenStepsSeconds = 10,
            MaximumPauseBetweenStepsSeconds = 20
        };

        Assert.That(settings.EffectivePauseRange, Is.EqualTo(new PauseRange(10, 20)));
    }

    [Test]
    public void TheBatchProcessingTakesItsBoundsFromItsSettings()
    {
        var storedBelowTheLimit = new DataBatchProcessing
        {
            MinimumDelaySeconds = 0,
            MaximumDelaySeconds = 3
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new DataBatchProcessing().EffectivePauseRange, Is.EqualTo(new PauseRange(6, 10)));
            Assert.That(storedBelowTheLimit.EffectivePauseRange, Is.EqualTo(new PauseRange(6, 6)));
        }
    }

    [Test]
    public void OnlyZeroForBothBoundsMeansNoPause()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PauseRange.GetEffective(INDEXING, 0, UNLOCKED, 0, UNLOCKED).IsNone, Is.True);
            Assert.That(PauseRange.GetEffective(INDEXING, 0, UNLOCKED, 5, UNLOCKED).IsNone, Is.False);
            Assert.That(PauseRange.GetEffective(INDEXING, 30, UNLOCKED, 0, UNLOCKED), Is.EqualTo(new PauseRange(30, 30)));
            Assert.That(new DataDataSourceIndexing().EffectivePauseRange.IsNone, Is.True);
        }
    }

    [Test]
    public void TheBatchProcessingAlwaysPauses()
    {
        Assert.That(PauseRange.GetEffective(BATCH_PROCESSING, 0, UNLOCKED, 0, UNLOCKED).IsNone, Is.False);
    }

    [Test]
    public void TheLimitsAllowTheirShortestAndLongestPause()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(INDEXING.Allows(0), Is.True);
            Assert.That(INDEXING.Allows(-1), Is.False);
            Assert.That(INDEXING.Allows(300), Is.True);
            Assert.That(INDEXING.Allows(301), Is.False);
            Assert.That(BATCH_PROCESSING.Allows(5), Is.False);
            Assert.That(BATCH_PROCESSING.Allows(6), Is.True);
            Assert.That(BATCH_PROCESSING.Allows(300), Is.True);
            Assert.That(BATCH_PROCESSING.Allows(301), Is.False);
        }
    }
}