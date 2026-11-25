using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how the choices of several runs are counted.
/// </summary>
[TestFixture]
public sealed class ModelComparisonTallyTests
{
    [Test]
    public void NoRunsCountNothing()
    {
        var tally = ModelComparisonTally.Of([]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tally.TotalRunCount, Is.Zero);
            Assert.That(tally.ScoredRunCount, Is.Zero);
            Assert.That(tally.UnscoredRunCount, Is.Zero);
            Assert.That(tally.FirstModelWins, Is.Zero);
            Assert.That(tally.SecondModelWins, Is.Zero);
            Assert.That(tally.Ties, Is.Zero);
        }
    }

    [Test]
    public void EveryChoiceCountsForItsModel()
    {
        var tally = ModelComparisonTally.Of(
        [
            ModelComparisonModelChoice.FIRST,
            ModelComparisonModelChoice.SECOND,
            ModelComparisonModelChoice.FIRST,
            ModelComparisonModelChoice.TIE,
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tally.TotalRunCount, Is.EqualTo(4));
            Assert.That(tally.ScoredRunCount, Is.EqualTo(4));
            Assert.That(tally.FirstModelWins, Is.EqualTo(2));
            Assert.That(tally.SecondModelWins, Is.EqualTo(1));
            Assert.That(tally.Ties, Is.EqualTo(1));
        }
    }

    [Test]
    public void ARunWithoutAChoiceCountsOnlyTowardTheTotal()
    {
        var tally = ModelComparisonTally.Of([ModelComparisonModelChoice.SECOND, null, null]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tally.TotalRunCount, Is.EqualTo(3), "Every run happened.");
            Assert.That(tally.ScoredRunCount, Is.EqualTo(1), "Only one run had a choice.");
            Assert.That(tally.UnscoredRunCount, Is.EqualTo(2));
            Assert.That(tally.SecondModelWins, Is.EqualTo(1));
        }
    }
}