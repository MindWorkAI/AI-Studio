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
    public void ThePromptCarriesTheUserInstructionsAndBothAnswers()
    {
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, "A good summary keeps every number.", "Answer from the first model.", "Answer from the second model.");

        var prompt = judgeRequest.ToPrompt();

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("A good summary keeps every number."));
            Assert.That(prompt, Does.Contain("Answer from the first model."));
            Assert.That(prompt, Does.Contain("Answer from the second model."));
            Assert.That(prompt, Does.Contain(REQUEST.ToPrompt()));
        });
    }

    [Test]
    public void WhiteSpaceAroundTheInstructionsIsDropped()
    {
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, "  Keep it short.  \n", "First.", "Second.");

        Assert.That(judgeRequest.Instructions, Is.EqualTo("Keep it short."));
    }

    [Test]
    public void WithoutInstructionsThePromptStillCarriesAGenericCriterion()
    {
        // The judge already sees the request and both answers, so the field is a refinement, not a requirement:
        var judgeRequest = new ModelComparisonJudgeRequest(REQUEST, string.Empty, "Answer from the first model.", "Answer from the second model.");

        var prompt = judgeRequest.ToPrompt();

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("better answers the question or better completes the task"));
            Assert.That(prompt, Does.Contain("Answer from the first model."));
            Assert.That(prompt, Does.Contain("Answer from the second model."));
            Assert.That(prompt, Does.Not.Contain("Pay particular attention to this:"));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("# Answer A"));
            Assert.That(prompt, Does.Contain("# Answer B"));
            Assert.That(prompt, Does.Not.Contain("# Answer 1"));
            Assert.That(prompt, Does.Not.Contain("# Answer 2"));
        });
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
