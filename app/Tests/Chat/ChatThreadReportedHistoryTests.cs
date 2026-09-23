using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks when what a provider reported for a conversation still describes it.
/// </summary>
/// <remarks>
/// A reported number is shown as exact, so a wrong one does more harm than no number at all: it
/// tells a person that their chat fits while it does not. Every case below is one where the thread
/// moved on after the report, and the estimate has to take over -- or one where it came back to
/// exactly the state the report was taken in, and the report counts again.
/// </remarks>
[TestFixture]
public sealed class ChatThreadReportedHistoryTests
{
    private static readonly DateTimeOffset START = new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    private static readonly Model MODEL = new("model-a", null);

    private static readonly Model OTHER_MODEL = new("model-b", null);

    [Test]
    public void TheReportOfTheLastAnswerCounts()
    {
        var thread = Thread(Question(1), Answer(2, promptTokens: 1200, blockCount: 2));

        var history = thread.ReportedHistoryFor(MODEL);

        Assert.Multiple(() =>
        {
            Assert.That(history.IsKnown, Is.True);
            Assert.That(history.PromptTokens, Is.EqualTo(1200));

            //
            // The answer comes along as text rather than as the provider's number for it, which
            // would include the reasoning the next request never carries:
            //
            Assert.That(history.LastAnswer, Is.EqualTo("Answer 2"));
        });
    }

    [Test]
    public void AnAnswerWithoutTextAddsNothingToThePrompt()
    {
        //
        // An answer which consists of reasoning only. It may be kept to be read, but without any
        // text it is never sent, so what the provider counted is all the next request carries of
        // the conversation so far.
        //
        var reasoningOnly = new ContentText { Text = string.Empty };
        reasoningOnly.RecordReportedUsage(TokenUsage.Of(1200), MODEL.Id, 2);
        var thread = Thread(Question(1), Block(ChatRole.AI, reasoningOnly, 2));

        var history = thread.ReportedHistoryFor(MODEL);
        Assert.Multiple(() =>
        {
            Assert.That(history.IsKnown, Is.True);
            Assert.That(history.PromptTokens, Is.EqualTo(1200));
            Assert.That(history.LastAnswer, Is.Empty);
        });
    }

    [Test]
    public void AMessageWhoseRequestFailedIsNotLeftOut()
    {
        //
        // A request which was turned down leaves the question standing and removes the empty
        // answer. The report before it knows nothing about that question -- which may well be the
        // very message that made the chat too large.
        //
        var thread = Thread(Question(1), Answer(2, promptTokens: 1200, blockCount: 2), Question(3));

        Assert.That(thread.ReportedHistoryFor(MODEL).IsKnown, Is.False);
    }

    [Test]
    public void AnAnswerBeingWrittenDoesNotBorrowTheReportBeforeIt()
    {
        var streaming = Block(ChatRole.AI, new ContentText { Text = "Half an ans", IsStreaming = true }, 4);
        var thread = Thread(Question(1), Answer(2, promptTokens: 1200, blockCount: 2), Question(3), streaming);

        Assert.That(thread.ReportedHistoryFor(MODEL).IsKnown, Is.False);
    }

    [Test]
    public void AnAnswerWithoutAReportDoesNotBorrowTheReportBeforeIt()
    {
        //
        // What an answer written while tools were offered looks like today: finished, but without
        // a report of its own.
        //
        var withoutReport = Block(ChatRole.AI, new ContentText { Text = "Second answer" }, 4);
        var thread = Thread(Question(1), Answer(2, promptTokens: 1200, blockCount: 2), Question(3), withoutReport);

        Assert.That(thread.ReportedHistoryFor(MODEL).IsKnown, Is.False);
    }

    [Test]
    public void DeletingAnEarlierMessageOutdatesTheReport()
    {
        //
        // Deleting a large message to make room is exactly when somebody watches the number, and
        // the deleted message is still inside what the provider reported.
        //
        var firstQuestion = Question(1);
        var thread = Thread(firstQuestion, Answer(2, promptTokens: 1200, blockCount: 2), Question(3), Answer(4, promptTokens: 2600, blockCount: 4));

        thread.Remove(firstQuestion.Content!);

        Assert.That(thread.ReportedHistoryFor(MODEL).IsKnown, Is.False);
    }

    [Test]
    public void AnotherModelOutdatesTheReport()
    {
        var thread = Thread(Question(1), Answer(2, promptTokens: 1200, blockCount: 2));

        Assert.That(thread.ReportedHistoryFor(OTHER_MODEL).IsKnown, Is.False);
    }

    [Test]
    public void EditingTheLastMessageBringsTheReportBeforeItBack()
    {
        var lastQuestion = Question(3);
        var lastAnswer = Answer(4, promptTokens: 2600, blockCount: 4);
        var thread = Thread(Question(1), Answer(2, promptTokens: 1200, blockCount: 2), lastQuestion, lastAnswer);

        //
        // What editing the last message does to the thread: the question goes back into the
        // composer, and its answer goes with it.
        //
        thread.Remove(lastQuestion.Content!);
        thread.Remove(lastAnswer.Content!);

        var history = thread.ReportedHistoryFor(MODEL);
        Assert.Multiple(() =>
        {
            Assert.That(history.IsKnown, Is.True, "The first answer is the last block again, with exactly the blocks it was reported for.");
            Assert.That(history.PromptTokens, Is.EqualTo(1200));
            Assert.That(history.LastAnswer, Is.EqualTo("Answer 2"));
        });
    }

    [Test]
    public void RollingBackToAnAnswerBringsItsReportBack()
    {
        var firstAnswer = Answer(2, promptTokens: 1200, blockCount: 2);
        var thread = Thread(Question(1), firstAnswer, Question(3), Answer(4, promptTokens: 2600, blockCount: 4));

        thread.RollBackTo(firstAnswer.Content!);

        var history = thread.ReportedHistoryFor(MODEL);
        Assert.Multiple(() =>
        {
            Assert.That(history.IsKnown, Is.True);
            Assert.That(history.PromptTokens, Is.EqualTo(1200));
            Assert.That(history.LastAnswer, Is.EqualTo("Answer 2"));
        });
    }

    private static ChatThread Thread(params ContentBlock[] blocks) => new()
    {
        Blocks = [..blocks],
    };

    private static ContentBlock Question(int minute) => Block(ChatRole.USER, new ContentText { Text = $"Question {minute}" }, minute);

    private static ContentBlock Answer(int minute, int promptTokens, int blockCount)
    {
        var answer = new ContentText { Text = $"Answer {minute}" };
        answer.RecordReportedUsage(TokenUsage.Of(promptTokens), MODEL.Id, blockCount);
        return Block(ChatRole.AI, answer, minute);
    }

    private static ContentBlock Block(ChatRole role, ContentText content, int minute) => new()
    {
        Time = START.AddMinutes(minute),
        ContentType = ContentType.TEXT,
        Content = content,
        Role = role,
    };
}