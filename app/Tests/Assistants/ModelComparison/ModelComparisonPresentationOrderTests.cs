using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks which answer stands in which column.
/// </summary>
/// <remarks>
/// A wrong assignment does not crash. It shows the answer of one model under the name of the other
/// once the vote is in, and nothing on the screen looks off.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonPresentationOrderTests
{
    [TestCase(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, "The first model", "The second model")]
    [TestCase(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST, "The second model", "The first model")]
    public void TheColumnsShowTheAnswersOfTheModelsTheOrderSays(ModelComparisonPresentationOrder order, string expectedInColumnA, string expectedInColumnB)
    {
        var run = CreateRunResult();

        Assert.Multiple(() =>
        {
            Assert.That(order.InColumnA(run).Label, Is.EqualTo(expectedInColumnA));
            Assert.That(order.InColumnB(run).Label, Is.EqualTo(expectedInColumnB));
        });
    }

    [Test]
    public void TheTextOfAColumnIsTheTextOfTheModelWhoseNameItCarries()
    {
        // The name and the text travel together in one answer, so a column can never mix them up:
        var run = CreateRunResult();

        foreach (var order in Enum.GetValues<ModelComparisonPresentationOrder>())
        {
            Assert.That(order.InColumnA(run).Text, Does.Contain(order.InColumnA(run).Label), $"{order}, column A");
            Assert.That(order.InColumnB(run).Text, Does.Contain(order.InColumnB(run).Label), $"{order}, column B");
        }
    }

    [Test]
    public void AnswersInTheTwoColumnsAreNeverTheSame()
    {
        var run = CreateRunResult();

        foreach (var order in Enum.GetValues<ModelComparisonPresentationOrder>())
            Assert.That(order.InColumnA(run).Label, Is.Not.EqualTo(order.InColumnB(run).Label), order.ToString());
    }

    [Test]
    public void EveryOrderIsKnown()
    {
        var run = CreateRunResult();

        foreach (var order in Enum.GetValues<ModelComparisonPresentationOrder>())
        {
            Assert.DoesNotThrow(() => order.InColumnA(run), order.ToString());
            Assert.DoesNotThrow(() => order.InColumnB(run), order.ToString());
        }
    }

    [TestCase(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, ModelComparisonVote.COLUMN_A, ModelComparisonModelChoice.FIRST)]
    [TestCase(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, ModelComparisonVote.COLUMN_B, ModelComparisonModelChoice.SECOND)]
    [TestCase(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST, ModelComparisonVote.COLUMN_A, ModelComparisonModelChoice.SECOND)]
    [TestCase(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST, ModelComparisonVote.COLUMN_B, ModelComparisonModelChoice.FIRST)]
    [TestCase(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, ModelComparisonVote.TIE, ModelComparisonModelChoice.TIE)]
    [TestCase(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST, ModelComparisonVote.TIE, ModelComparisonModelChoice.TIE)]
    public void AColumnBasedVoteIsTranslatedBackToItsModel(ModelComparisonPresentationOrder order, ModelComparisonVote vote, ModelComparisonModelChoice expected)
    {
        Assert.That(order.ToModelChoice(vote), Is.EqualTo(expected));
    }

    [Test]
    public void TranslatingAVoteAndBackAgainIsConsistentWithTheColumns()
    {
        // Whichever model a column names, translating that model's vote back must land on the model actually standing there:
        var run = CreateRunResult();

        foreach (var order in Enum.GetValues<ModelComparisonPresentationOrder>())
        {
            var modelInColumnA = order.InColumnA(run).Label;
            var choiceForColumnA = order.ToModelChoice(ModelComparisonVote.COLUMN_A);
            var expectedLabel = choiceForColumnA is ModelComparisonModelChoice.FIRST ? "The first model" : "The second model";

            Assert.That(modelInColumnA, Is.EqualTo(expectedLabel), order.ToString());
        }
    }

    private static ModelComparisonRunResult CreateRunResult() => new()
    {
        First = new ModelComparisonAnswer
        {
            Label = "The first model",
            Text = "The answer of The first model.",
            Completed = true,
        },
        Second = new ModelComparisonAnswer
        {
            Label = "The second model",
            Text = "The answer of The second model.",
            Completed = true,
        },
    };
}