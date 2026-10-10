using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how the judge's raw answer is turned into a verdict.
/// </summary>
/// <remarks>
/// The judge is asked for one JSON object and nothing else, but a model rarely holds to that
/// perfectly. Parsing has to look past a stray sentence around the object, and has to end up with a
/// verdict which is not <see cref="ModelComparisonJudgeVerdict.Completed"/> rather than throw when
/// there is nothing usable to read.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonJudgeVerdictTests
{
    [Test]
    public void APlainJsonObjectIsRead()
    {
        var verdict = ModelComparisonJudgeVerdict.Parse("Judge", true, """{"preferred": "A", "reasoning": "It kept every number from the document."}""");

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Completed, Is.True);
            Assert.That(verdict.Label, Is.EqualTo("Judge"));
            Assert.That(verdict.Preferred, Is.EqualTo(ModelComparisonVote.COLUMN_A));
            Assert.That(verdict.Reasoning, Is.EqualTo("It kept every number from the document."));
        });
    }

    [Test]
    public void APreferenceIsReadWithoutRegardToCase()
    {
        var verdict = ModelComparisonJudgeVerdict.Parse("Judge", true, """{"preferred": "tie", "reasoning": "Both cover the same points."}""");

        Assert.That(verdict.Preferred, Is.EqualTo(ModelComparisonVote.TIE));
    }

    [Test]
    public void TextAroundTheJsonObjectIsIgnored()
    {
        var verdict = ModelComparisonJudgeVerdict.Parse("Judge", true, """
            Sure, here is my verdict:
            ```json
            {"preferred": "B", "reasoning": "It is shorter and just as complete."}
            ```
            """);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Completed, Is.True);
            Assert.That(verdict.Preferred, Is.EqualTo(ModelComparisonVote.COLUMN_B));
        });
    }

    [Test]
    public void AJudgeWhichDidNotAnswerIsNotCompleted()
    {
        var verdict = ModelComparisonJudgeVerdict.Parse("Judge", false, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Completed, Is.False);
            Assert.That(verdict.Preferred, Is.Null);
            Assert.That(verdict.Reasoning, Is.Empty);
        });
    }

    [TestCase("Sorry, I cannot help with that.")]
    [TestCase("""{"reasoning": "Missing the preference field."}""")]
    [TestCase("""{"preferred": "C", "reasoning": "Not one of the three allowed values."}""")]
    [TestCase("""{"preferred": "A", "reasoning": }""")]
    public void AnUnreadableAnswerIsNotCompleted(string judgeText)
    {
        var verdict = ModelComparisonJudgeVerdict.Parse("Judge", true, judgeText);

        Assert.That(verdict.Completed, Is.False);
    }
}
