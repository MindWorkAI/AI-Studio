using AIStudio.Assistants.ModelComparison;
using AIStudio.Tools;

using MudBlazor;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how the summary of a batch shows what the user and the judge chose.
/// </summary>
[TestFixture]
public sealed class ModelComparisonOutcomeTests
{
    [Test]
    public void WithoutAVoteTheCardOnlyFillsItsColumn()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModelComparisonOutcome.CardClassOf(null, ModelComparisonModelChoice.FIRST), Is.EqualTo(ModelComparisonOutcome.FILL_THE_COLUMN));
            Assert.That(ModelComparisonOutcome.ColorOf(null, ModelComparisonModelChoice.FIRST), Is.EqualTo(Color.Default));
            Assert.That(ModelComparisonOutcome.IconOf(null, ModelComparisonModelChoice.FIRST), Is.EqualTo(AppIcons.VOTE_MISSING));
        }
    }

    [TestCase(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.FIRST, Color.Success, "mud-border-success")]
    [TestCase(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.SECOND, Color.Error, "mud-border-error")]
    [TestCase(ModelComparisonModelChoice.TIE, ModelComparisonModelChoice.SECOND, Color.Warning, "mud-border-warning")]
    public void TheVoteColorsTheCardOfEachModel(ModelComparisonModelChoice vote, ModelComparisonModelChoice model, Color expectedColor, string expectedBorder)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModelComparisonOutcome.ColorOf(vote, model), Is.EqualTo(expectedColor));
            Assert.That(ModelComparisonOutcome.CardClassOf(vote, model), Does.StartWith(ModelComparisonOutcome.FILL_THE_COLUMN).And.EndWith(expectedBorder));
        }
    }

    [Test]
    public void EveryOutcomeHasAnIconOfItsOwn()
    {
        var icons = new[]
        {
            ModelComparisonOutcome.IconOf(null, ModelComparisonModelChoice.FIRST),
            ModelComparisonOutcome.IconOf(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.FIRST),
            ModelComparisonOutcome.IconOf(ModelComparisonModelChoice.SECOND, ModelComparisonModelChoice.FIRST),
            ModelComparisonOutcome.IconOf(ModelComparisonModelChoice.TIE, ModelComparisonModelChoice.FIRST),
        };

        Assert.That(icons, Is.Unique);
    }

    [TestCase(null, ModelComparisonModelChoice.FIRST, false)]
    [TestCase(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.FIRST, true)]
    [TestCase(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.SECOND, false)]
    [TestCase(ModelComparisonModelChoice.TIE, ModelComparisonModelChoice.FIRST, true)]
    [TestCase(ModelComparisonModelChoice.TIE, ModelComparisonModelChoice.SECOND, true)]
    public void TheJudgeFavorsItsChoiceOrBothOnATie(ModelComparisonModelChoice? judgeChoice, ModelComparisonModelChoice model, bool expected) => Assert.That(ModelComparisonOutcome.JudgeFavors(judgeChoice, model), Is.EqualTo(expected));

    [Test]
    public void ARunGivesTheAnswerOfEachModel()
    {
        var result = new ModelComparisonRunResult
        {
            First = new() { Label = "first", Text = "one", Completed = true },
            Second = new() { Label = "second", Text = "two", Completed = true },
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.AnswerOf(ModelComparisonModelChoice.FIRST), Is.SameAs(result.First));
            Assert.That(result.AnswerOf(ModelComparisonModelChoice.SECOND), Is.SameAs(result.Second));
            Assert.Throws<ArgumentOutOfRangeException>(() => result.AnswerOf(ModelComparisonModelChoice.TIE));
        }
    }
}