using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks that a judge starts only by the expectations, values, and goals of the user, or with the confirmation that it
/// may decide by its own standards.
/// </summary>
[TestFixture]
public sealed class ModelComparisonJudgeExpectationsTests
{
    [TestCase("", false, true)]
    [TestCase("   ", false, true)]
    [TestCase("", true, false)]
    [TestCase("I prefer short answers.", false, false)]
    [TestCase("I prefer short answers.", true, false)]
    public void WithoutExpectationsTheUserHasToConfirm(string expectations, bool ownStandardsAccepted, bool expectedIssue)
    {
        Assert.That(ModelComparisonJudgeExpectations.IssueOf(expectations, ownStandardsAccepted) is not null, Is.EqualTo(expectedIssue));
    }

    [Test]
    public void ThePromptOfAJudgeGetsTheExpectationsOnlyWhenThereAreSome()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModelComparisonJudgeFormat.ExpectationsOfTheUser("  "), Is.Empty);
            Assert.That(ModelComparisonJudgeFormat.ExpectationsOfTheUser(" I prefer short answers. "), Does.EndWith("# Expectations, values, and goals of the user\nI prefer short answers."));
        }
    }
}