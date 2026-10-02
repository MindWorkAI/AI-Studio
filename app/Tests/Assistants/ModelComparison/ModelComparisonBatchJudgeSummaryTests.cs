using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how the judge's verdicts across a batch are tallied.
/// </summary>
/// <remarks>
/// The point of the tally is that it survives the presentation order changing from run to run: a
/// model which won column A in one run and column B in the next must still be counted as the same
/// winner both times.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonBatchJudgeSummaryTests
{
    [Test]
    public void AnEmptyBatchScoresNothing()
    {
        var summary = ModelComparisonBatchJudgeSummary.From([]);

        Assert.Multiple(() =>
        {
            Assert.That(summary.TotalRunCount, Is.Zero);
            Assert.That(summary.ScoredRunCount, Is.Zero);
            Assert.That(summary.FirstModelWins, Is.Zero);
            Assert.That(summary.SecondModelWins, Is.Zero);
            Assert.That(summary.Ties, Is.Zero);
        });
    }

    [Test]
    public void WinsAreCountedByModelNotByColumn()
    {
        // The first model stands in column A twice and in column B once, and wins every time -- the tally has to follow the model, not the column:
        var entries = new[]
        {
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, ModelComparisonVote.COLUMN_A),
            Entry(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST, ModelComparisonVote.COLUMN_B),
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, ModelComparisonVote.COLUMN_A),
        };

        var summary = ModelComparisonBatchJudgeSummary.From(entries);

        Assert.Multiple(() =>
        {
            Assert.That(summary.TotalRunCount, Is.EqualTo(3));
            Assert.That(summary.ScoredRunCount, Is.EqualTo(3));
            Assert.That(summary.FirstModelWins, Is.EqualTo(3));
            Assert.That(summary.SecondModelWins, Is.Zero);
            Assert.That(summary.Ties, Is.Zero);
        });
    }

    [Test]
    public void ATieIsCountedAsATieRegardlessOfOrder()
    {
        var entries = new[]
        {
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, ModelComparisonVote.TIE),
            Entry(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST, ModelComparisonVote.TIE),
        };

        var summary = ModelComparisonBatchJudgeSummary.From(entries);

        Assert.That(summary.Ties, Is.EqualTo(2));
    }

    [Test]
    public void ARunWithoutACompletedVerdictIsNotScored()
    {
        var entries = new[]
        {
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, ModelComparisonVote.COLUMN_A),
            EntryWithoutAVerdict(),
        };

        var summary = ModelComparisonBatchJudgeSummary.From(entries);

        Assert.Multiple(() =>
        {
            Assert.That(summary.TotalRunCount, Is.EqualTo(2), "Both runs happened.");
            Assert.That(summary.ScoredRunCount, Is.EqualTo(1), "Only one run had a usable verdict.");
            Assert.That(summary.FirstModelWins, Is.EqualTo(1));
        });
    }

    private static ModelComparisonBatchEntry Entry(ModelComparisonPresentationOrder order, ModelComparisonVote judgePreferred) => new()
    {
        PresentationOrder = order,
        Result = new ModelComparisonRunResult
        {
            First = Answer("First"),
            Second = Answer("Second"),
            Judge = new ModelComparisonJudgeVerdict
            {
                Label = "Judge",
                Completed = true,
                Preferred = judgePreferred,
            },
        },
    };

    private static ModelComparisonBatchEntry EntryWithoutAVerdict() => new()
    {
        PresentationOrder = ModelComparisonPresentationOrder.FIRST_MODEL_FIRST,
        Result = new ModelComparisonRunResult
        {
            First = Answer("First"),
            Second = Answer("Second"),
            Judge = new ModelComparisonJudgeVerdict
            {
                Label = "Judge",
                Completed = false,
            },
        },
    };

    private static ModelComparisonAnswer Answer(string label) => new()
    {
        Label = label,
        Text = $"The answer of {label}.",
        Completed = true,
    };
}
