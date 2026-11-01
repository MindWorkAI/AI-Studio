using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which bounds of the pause between two indexing steps apply, as the user set them and as configuration plugins lock them.
/// </summary>
[TestFixture]
public sealed class PauseRangeTests
{
    [Test]
    public void BoundsInOrderStayAsTheyAre()
    {
        Assert.That(PauseRange.GetEffective(10, false, 20, false), Is.EqualTo(new PauseRange(10, 20)));
    }

    [Test]
    public void AnUpperBoundBelowTheLowerOneIsRaisedToIt()
    {
        Assert.That(PauseRange.GetEffective(60, false, 20, false), Is.EqualTo(new PauseRange(60, 60)));
    }

    [Test]
    public void ALockedLowerBoundRaisesTheUpperOne()
    {
        Assert.That(PauseRange.GetEffective(60, true, 20, false), Is.EqualTo(new PauseRange(60, 60)));
    }

    [Test]
    public void ALockedUpperBoundLowersTheLowerOne()
    {
        Assert.That(PauseRange.GetEffective(60, false, 20, true), Is.EqualTo(new PauseRange(20, 20)));
    }

    [Test]
    public void BothBoundsLockedTheWrongWayRoundLetTheLongerPauseWin()
    {
        Assert.That(PauseRange.GetEffective(60, true, 20, true), Is.EqualTo(new PauseRange(60, 60)));
    }

    [Test]
    public void BoundsOutsideTheAllowedRangeAreClamped()
    {
        Assert.That(PauseRange.GetEffective(-5, false, 1_000, false), Is.EqualTo(new PauseRange(0, DataDataSourceIndexing.MAX_PAUSE_BETWEEN_STEPS_SECONDS)));
    }

    [Test]
    public void BoundsInOrderComeFromTheSettingsWithoutAskingForLocks()
    {
        var settings = new DataDataSourceIndexing
        {
            MinimumPauseBetweenStepsSeconds = 10,
            MaximumPauseBetweenStepsSeconds = 20
        };

        Assert.That(PauseRange.FromSettings(settings), Is.EqualTo(new PauseRange(10, 20)));
    }

    [Test]
    public void OnlyZeroForBothBoundsMeansNoPause()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PauseRange.GetEffective(0, false, 0, false).IsNone, Is.True);
            Assert.That(PauseRange.GetEffective(0, false, 5, false).IsNone, Is.False);
            Assert.That(PauseRange.GetEffective(30, false, 0, false), Is.EqualTo(new PauseRange(30, 30)));
            Assert.That(new DataDataSourceIndexing().MinimumPauseBetweenStepsSeconds, Is.Zero);
            Assert.That(new DataDataSourceIndexing().MaximumPauseBetweenStepsSeconds, Is.Zero);
        }
    }
}