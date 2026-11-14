using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks the prompt the judge is asked with.
/// </summary>
[TestFixture]
public sealed class ModelComparisonJudgeRequestTests
{
    private static readonly ModelComparisonRequest REQUEST = new("The document.", "Summarize it.");

    [Test]
    public void ThePromptCarriesTheExpectationsOfTheUserAndBothAnswers()
    {
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, "I prefer short answers.", "Answer from the first model.", "Answer from the second model.");

        var prompt = judgeRequest.ToPrompt();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("# Expectations, values, and goals of the user\nI prefer short answers."));
            Assert.That(prompt, Does.Contain("before any preference of your own"), "The standards of the user come first.");
            Assert.That(prompt, Does.Contain("Answer from the first model."));
            Assert.That(prompt, Does.Contain("Answer from the second model."));
            Assert.That(prompt, Does.Contain(REQUEST.ToPrompt(headingLevel: 2)), "The request stands below the heading of the judge prompt.");
            Assert.That(prompt.ReplaceLineEndings("\n"), Does.Not.Contain("\n# Material"), "The headings of the request must not stand on the level of the answers.");
        }
    }

    [Test]
    public void WhiteSpaceAroundTheInstructionsIsDropped()
    {
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, "  Keep it short.  \n", "First.", "Second.");

        Assert.That(judgeRequest.Instructions, Is.EqualTo("Keep it short."));
    }

    [Test]
    public void WithoutExpectationsThePromptCarriesTheGenericCriterionAlone()
    {
        // The user confirmed that the judge may decide by its own standards:
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, string.Empty, "Answer from the first model.", "Answer from the second model.");

        var prompt = judgeRequest.ToPrompt();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("better answers the question or better completes the task"));
            Assert.That(prompt, Does.Contain("Answer from the first model."));
            Assert.That(prompt, Does.Contain("Answer from the second model."));
            Assert.That(prompt, Does.Not.Contain("Expectations, values, and goals of the user"));
        }
    }

    [Test]
    public void WithoutALanguageNameThePromptAsksForNoParticularOne()
    {
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, string.Empty, "First.", "Second.");

        Assert.That(judgeRequest.ToPrompt(), Does.Not.Contain("Write the reasoning in"));
    }

    [Test]
    public void ALanguageNameTellsTheJudgeWhatToWriteIn()
    {
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, string.Empty, "First.", "Second.", "German");

        Assert.That(judgeRequest.ToPrompt(), Does.Contain("Write the reasoning in German"));
    }

    [Test]
    public void TheAnswersAreLabeledAAndBNotByModelOrder()
    {
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, string.Empty, "Answer from the first model.", "Answer from the second model.");

        var prompt = judgeRequest.ToPrompt();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("# Answer A"));
            Assert.That(prompt, Does.Contain("# Answer B"));
            Assert.That(prompt, Does.Not.Contain("# Answer 1"));
            Assert.That(prompt, Does.Not.Contain("# Answer 2"));
        }
    }

    [Test]
    public void WithoutACacheBusterThePromptIsUnchanged()
    {
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, string.Empty, "First.", "Second.");

        Assert.That(judgeRequest.ToPrompt(), Is.EqualTo(judgeRequest.ToPrompt(string.Empty)));
    }

    [Test]
    public void TwoDifferentCacheBustersBuildDifferentPrompts()
    {
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, string.Empty, "First.", "Second.");

        Assert.That(judgeRequest.ToPrompt("one"), Is.Not.EqualTo(judgeRequest.ToPrompt("two")));
    }
}
