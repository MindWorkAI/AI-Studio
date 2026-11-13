using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks the share of wins and its Wilson score interval, against values computed by hand.
/// </summary>
[TestFixture]
public sealed class ModelComparisonWinShareTests
{
    private const double TOLERANCE = 0.001;

    [Test]
    public void WithoutDecidedRunsNothingIsClear()
    {
        var winShare = ModelComparisonWinShare.Of(0, 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winShare.Share, Is.Null);
            Assert.That(winShare.LowerBound, Is.Null);
            Assert.That(winShare.UpperBound, Is.Null);
            Assert.That(winShare.Significance, Is.EqualTo(ModelComparisonSignificance.NOT_YET_CLEAR));
        }
    }

    [Test]
    public void EightOfTenIsNotYetClear()
    {
        var winShare = ModelComparisonWinShare.Of(8, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winShare.Share, Is.EqualTo(0.8).Within(TOLERANCE));
            Assert.That(winShare.LowerBound, Is.EqualTo(0.4902).Within(TOLERANCE));
            Assert.That(winShare.UpperBound, Is.EqualTo(0.9433).Within(TOLERANCE));
            Assert.That(winShare.Significance, Is.EqualTo(ModelComparisonSignificance.NOT_YET_CLEAR), "The interval still reaches below one half.");
        }
    }

    [Test]
    public void EighteenOfTwentyIsClearlyBetter()
    {
        var winShare = ModelComparisonWinShare.Of(18, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winShare.LowerBound, Is.EqualTo(0.6990).Within(TOLERANCE));
            Assert.That(winShare.Significance, Is.EqualTo(ModelComparisonSignificance.CLEARLY_BETTER));
        }
    }

    [Test]
    public void NoneOfTenIsClearlyWorse()
    {
        var winShare = ModelComparisonWinShare.Of(0, 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winShare.LowerBound, Is.EqualTo(0).Within(TOLERANCE));
            Assert.That(winShare.UpperBound, Is.EqualTo(0.2775).Within(TOLERANCE));
            Assert.That(winShare.Significance, Is.EqualTo(ModelComparisonSignificance.CLEARLY_WORSE));
        }
    }

    [Test]
    public void ANegativeCountIsRefused()
    {
        Assert.That(() => ModelComparisonWinShare.Of(-1, 3), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}