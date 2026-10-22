using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks the text both models receive.
/// </summary>
/// <remarks>
/// A difference in this text would be measured as a difference between the models, so what matters
/// is that the shape stays the same, and that a request without a context is not dressed up.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonRequestTests
{
    [Test]
    public void AQuestionAloneIsSentAsItIs()
    {
        var request = new ModelComparisonRequest(string.Empty, "What is the capital of France?");

        Assert.That(request.ToPrompt(), Is.EqualTo("What is the capital of France?"));
    }

    [Test]
    public void AContextAndAQuestionAreSeparatedByHeadings()
    {
        var request = new ModelComparisonRequest("The document.", "Summarize it.");

        Assert.That(request.ToPrompt(), Is.EqualTo("# Context\nThe document.\n\n# Question or task\nSummarize it.").Or.EqualTo("# Context\r\nThe document.\r\n\r\n# Question or task\r\nSummarize it."));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\r\n\t\n")]
    public void AContextOfWhiteSpaceCountsAsNone(string context)
    {
        var request = new ModelComparisonRequest(context, "Summarize it.");

        Assert.Multiple(() =>
        {
            Assert.That(request.Context, Is.Empty);
            Assert.That(request.ToPrompt(), Is.EqualTo("Summarize it."));
        });
    }

    [Test]
    public void WhiteSpaceAroundTheTextsIsDropped()
    {
        var request = new ModelComparisonRequest("\n\nThe document.\n", "  Summarize it.  ");

        Assert.Multiple(() =>
        {
            Assert.That(request.Context, Is.EqualTo("The document."));
            Assert.That(request.Question, Is.EqualTo("Summarize it."));
        });
    }

    [Test]
    public void AShortQuestionIsItsOwnPreview()
    {
        var request = new ModelComparisonRequest(string.Empty, "Summarize it.");

        Assert.That(request.GetQuestionPreview(300), Is.EqualTo("Summarize it."));
    }

    [Test]
    public void ALongQuestionIsCutAndMarked()
    {
        var request = new ModelComparisonRequest(string.Empty, "Summarize this document in a few short sentences.");

        Assert.That(request.GetQuestionPreview(18), Is.EqualTo("Summarize this doc…"));
    }

    [Test]
    public void ACutDoesNotLeaveHalfOfACharacter()
    {
        // The emoji is two chars long. A cut after the first of them would leave a lone surrogate:
        var request = new ModelComparisonRequest(string.Empty, "aaaa😀bbb");

        Assert.That(request.GetQuestionPreview(5), Is.EqualTo("aaaa…"));
    }

    [Test]
    public void ThePreviewOfAnEmptyLengthIsRefused()
    {
        var request = new ModelComparisonRequest(string.Empty, "Summarize it.");

        Assert.Throws<ArgumentOutOfRangeException>(() => request.GetQuestionPreview(0));
    }

    [Test]
    public void TheSameInputAlwaysBuildsTheSamePrompt()
    {
        var first = new ModelComparisonRequest("The document.", "Summarize it.");
        var second = new ModelComparisonRequest("The document.", "Summarize it.");

        Assert.That(first.ToPrompt(), Is.EqualTo(second.ToPrompt()));
    }

    [Test]
    public void WithoutALanguageNameThePromptNamesNone()
    {
        var request = new ModelComparisonRequest(string.Empty, "Summarize it.");

        Assert.That(request.ToPrompt(), Does.Not.Contain("Answer in"));
    }

    [Test]
    public void ALanguageNameIsAppendedAsAnInstruction()
    {
        var request = new ModelComparisonRequest(string.Empty, "Summarize it.", "German");

        Assert.That(request.ToPrompt(), Is.EqualTo("Summarize it.\n\nAnswer in German."));
    }

    [Test]
    public void WithoutACacheBusterThePromptIsUnchanged()
    {
        var request = new ModelComparisonRequest(string.Empty, "Summarize it.");

        Assert.That(request.ToPrompt(), Is.EqualTo(request.ToPrompt(string.Empty)));
    }

    [Test]
    public void ACacheBusterIsAppendedAsAPlainlyLabeledLine()
    {
        var request = new ModelComparisonRequest(string.Empty, "Summarize it.");

        var prompt = request.ToPrompt("a1b2c3");

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.StartWith("Summarize it."));
            Assert.That(prompt, Does.Contain("a1b2c3"));
        });
    }

    [Test]
    public void TwoDifferentCacheBustersBuildDifferentPrompts()
    {
        var request = new ModelComparisonRequest(string.Empty, "Summarize it.");

        Assert.That(request.ToPrompt("one"), Is.Not.EqualTo(request.ToPrompt("two")));
    }
}