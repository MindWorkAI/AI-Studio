using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how the user's own votes across a batch are tallied.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ModelComparisonBatchJudgeSummaryTests"/>: the tally has to survive the
/// presentation order changing from run to run, and a run without a vote must not be counted.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonBatchVoteSummaryTests
{
    [Test]
    public void AnEmptyBatchScoresNothing()
    {
        var summary = ModelComparisonBatchVoteSummary.From([], []);

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
        var entries = new[]
        {
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST),
            Entry(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST),
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST),
        };

        // The first model stands in column A twice and in column B once, and the user votes for it every time:
        var votes = new ModelComparisonVote?[]
        {
            ModelComparisonVote.COLUMN_A,
            ModelComparisonVote.COLUMN_B,
            ModelComparisonVote.COLUMN_A,
        };

        var summary = ModelComparisonBatchVoteSummary.From(entries, votes);

        Assert.Multiple(() =>
        {
            Assert.That(summary.TotalRunCount, Is.EqualTo(3));
            Assert.That(summary.ScoredRunCount, Is.EqualTo(3));
            Assert.That(summary.FirstModelWins, Is.EqualTo(3));
            Assert.That(summary.SecondModelWins, Is.Zero);
        });
    }

    [Test]
    public void ARunWithoutAVoteIsNotScored()
    {
        var entries = new[]
        {
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST),
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST),
        };

        var votes = new ModelComparisonVote?[] { ModelComparisonVote.COLUMN_A, null };

        var summary = ModelComparisonBatchVoteSummary.From(entries, votes);

        Assert.Multiple(() =>
        {
            Assert.That(summary.TotalRunCount, Is.EqualTo(2), "Both runs happened.");
            Assert.That(summary.ScoredRunCount, Is.EqualTo(1), "Only one run was voted on.");
            Assert.That(summary.FirstModelWins, Is.EqualTo(1));
        });
    }

    [Test]
    public void AVoteWithNoMatchingEntryIndexIsNotScored()
    {
        // Fewer votes than entries -- the batch was left before reaching the rest:
        var entries = new[]
        {
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST),
            Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST),
        };

        var votes = new ModelComparisonVote?[] { ModelComparisonVote.TIE };

        var summary = ModelComparisonBatchVoteSummary.From(entries, votes);

        Assert.Multiple(() =>
        {
            Assert.That(summary.TotalRunCount, Is.EqualTo(2));
            Assert.That(summary.ScoredRunCount, Is.EqualTo(1));
            Assert.That(summary.Ties, Is.EqualTo(1));
        });
    }

    private static ModelComparisonBatchEntry Entry(ModelComparisonPresentationOrder order) => new()
    {
        PresentationOrder = order,
        Result = new ModelComparisonRunResult
        {
            First = Answer("First"),
            Second = Answer("Second"),
        },
    };

    private static ModelComparisonAnswer Answer(string label) => new()
    {
        Label = label,
        Text = $"The answer of {label}.",
        Completed = true,
    };
}
