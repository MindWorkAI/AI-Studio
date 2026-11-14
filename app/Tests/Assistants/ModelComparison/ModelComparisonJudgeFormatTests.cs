using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks that both judges of the Model Comparison assistant answer alike, each with the names the user sees.
/// </summary>
[TestFixture]
public sealed class ModelComparisonJudgeFormatTests
{
    [TestCase("A", ModelComparisonVote.COLUMN_A)]
    [TestCase(" b ", ModelComparisonVote.COLUMN_B)]
    [TestCase("tie", ModelComparisonVote.TIE)]
    [TestCase("1", null)]
    [TestCase(null, null)]
    public void TheJudgeOfAnswersNamesThemByLetter(string? preferred, ModelComparisonVote? expected) => Assert.That(ModelComparisonJudgeFormat.ANSWERS.VoteOf(preferred), Is.EqualTo(expected));

    [TestCase("1", ModelComparisonVote.COLUMN_A)]
    [TestCase("2", ModelComparisonVote.COLUMN_B)]
    [TestCase("TIE", ModelComparisonVote.TIE)]
    [TestCase("A", null)]
    [TestCase("3", null)]
    public void TheJudgeOfChatsNamesThemByNumber(string? preferred, ModelComparisonVote? expected) => Assert.That(ModelComparisonJudgeFormat.CHATS.VoteOf(preferred), Is.EqualTo(expected));

    [Test]
    public void AVerdictOnChatsIsReadWithTheirNumbers()
    {
        var verdict = ModelComparisonJudgeVerdict.Parse("judge", true, """{"preferred": "1", "reasoning": "Chat 1 was right."}""", ModelComparisonJudgeFormat.CHATS);
        var verdictWithLetter = ModelComparisonJudgeVerdict.Parse("judge", true, """{"preferred": "A", "reasoning": "n/a"}""", ModelComparisonJudgeFormat.CHATS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(verdict.Completed, Is.True);
            Assert.That(verdict.Preferred, Is.EqualTo(ModelComparisonVote.COLUMN_A));
            Assert.That(verdict.Reasoning, Is.EqualTo("Chat 1 was right."));
            Assert.That(verdictWithLetter.Completed, Is.False);
        }
    }

    [Test]
    public void BothJudgesGetTheSameShapeOfAnswer()
    {
        var answers = ModelComparisonJudgeFormat.ANSWERS.AnswerInstructions(string.Empty);
        var chats = ModelComparisonJudgeFormat.CHATS.AnswerInstructions("German");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answers, Does.Contain("""{"preferred": "A", "reasoning": """));
            Assert.That(answers, Does.Contain("""exactly one of "A", "B", or "TIE"."""));
            Assert.That(answers, Does.Contain("refer to each answer only by its letter, A or B"));
            Assert.That(answers, Does.Not.Contain("Write the reasoning in"));

            Assert.That(chats, Does.Contain("""{"preferred": "1", "reasoning": """));
            Assert.That(chats, Does.Contain("""exactly one of "1", "2", or "TIE"."""));
            Assert.That(chats, Does.Contain("refer to each chat only by its number, 1 or 2"));
            Assert.That(chats, Does.Contain("Write the reasoning in German"));
        }
    }

    [Test]
    public void TheJudgeOfChatsReadsBothChats()
    {
        // The prompt is a raw string literal, which keeps the line endings of the checkout:
        var prompt = new ModelComparisonChatJudgeRequest("[User]\nfirst", "[User]\nsecond", string.Empty, string.Empty).ToPrompt().ReplaceLineEndings("\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("# Chat 1\n[User]\nfirst"));
            Assert.That(prompt, Does.Contain("# Chat 2\n[User]\nsecond"));
            Assert.That(prompt, Does.Contain("must not guess, which model answered in which chat"));
            Assert.That(prompt, Does.Not.Contain("Expectations, values, and goals of the user"));
            Assert.That(prompt, Does.EndWith("not forced into English."));
        }
    }

    [Test]
    public void TheJudgeOfChatsDecidesByTheExpectationsOfTheUser()
    {
        var prompt = new ModelComparisonChatJudgeRequest("[User]\nfirst", "[User]\nsecond", string.Empty, "I prefer short answers.").ToPrompt().ReplaceLineEndings("\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("# Expectations, values, and goals of the user\nI prefer short answers.\n\n# Chat 1"), "The standards of the user stand before the chats.");
            Assert.That(prompt, Does.Contain("before any preference of your own"));
        }
    }

    [Test]
    public void ACacheBusterIsAnIgnorableLastLine()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModelComparisonJudgeFormat.WithCacheBuster("prompt", string.Empty), Is.EqualTo("prompt"));
            Assert.That(ModelComparisonJudgeFormat.WithCacheBuster("prompt", "abc"), Does.StartWith("prompt\n\n(Request ID: abc."));
        }
    }
}