using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how a task of a comparison series goes from the first chat over the second chat to the decision.
/// </summary>
[TestFixture]
public sealed class ModelComparisonSeriesTaskTests
{
    private static readonly DateTimeOffset BEGUN = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FIRST_SENT = BEGUN.AddMinutes(2);
    private static readonly DateTimeOffset FIRST_DONE = BEGUN.AddMinutes(12);
    private static readonly DateTimeOffset SECOND_SENT = BEGUN.AddMinutes(13);
    private static readonly DateTimeOffset SECOND_DONE = BEGUN.AddMinutes(20);
    private static readonly DateTimeOffset DECIDED = BEGUN.AddMinutes(25);

    [Test]
    public void ABegunTaskIsInTheFirstChat()
    {
        var task = ModelComparisonSeriesTask.Begin(BEGUN, firstModelIsChatOne: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(task.StartedAtUtc, Is.EqualTo(BEGUN));
            Assert.That(task.State, Is.EqualTo(ModelComparisonSeriesTaskState.FIRST_CHAT));
            Assert.That(task.CurrentChat, Is.SameAs(task.ChatOne));
            Assert.That(task.ChatOne.ChatId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(task.ChatTwo.ChatId, Is.Not.EqualTo(Guid.Empty).And.Not.EqualTo(task.ChatOne.ChatId));
            Assert.That(task.ChatOne.StartedAtUtc, Is.Null);
            Assert.That(task.IsBlind, Is.True);
        }
    }

    [Test]
    public void BothChatsKeepWhenTheirFirstMessageWasSentAndHowOftenTheUserWrote()
    {
        var task = ModelComparisonSeriesTask.Begin(BEGUN, firstModelIsChatOne: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.FinishFirstChat(FIRST_SENT, FIRST_DONE, 3, "Summarize the report."), Is.True);
            Assert.That(task.State, Is.EqualTo(ModelComparisonSeriesTaskState.SECOND_CHAT));
            Assert.That(task.CurrentChat, Is.SameAs(task.ChatTwo));
            Assert.That(task.QuestionPreview, Is.EqualTo("Summarize the report."));

            Assert.That(task.FinishSecondChat(SECOND_SENT, SECOND_DONE, 2), Is.True);
            Assert.That(task.State, Is.EqualTo(ModelComparisonSeriesTaskState.DECIDING));
            Assert.That(task.CurrentChat, Is.Null);

            Assert.That(task.ChatOne.Duration, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(task.ChatOne.UserMessageCount, Is.EqualTo(3));
            Assert.That(task.ChatTwo.Duration, Is.EqualTo(TimeSpan.FromMinutes(7)));
            Assert.That(task.ChatTwo.UserMessageCount, Is.EqualTo(2));
        }
    }

    [TestCase(true, ModelComparisonVote.COLUMN_A, ModelComparisonModelChoice.FIRST)]
    [TestCase(true, ModelComparisonVote.COLUMN_B, ModelComparisonModelChoice.SECOND)]
    [TestCase(false, ModelComparisonVote.COLUMN_A, ModelComparisonModelChoice.SECOND)]
    [TestCase(false, ModelComparisonVote.COLUMN_B, ModelComparisonModelChoice.FIRST)]
    [TestCase(false, ModelComparisonVote.TIE, ModelComparisonModelChoice.TIE)]
    public void TheDecisionNamesAModelOfTheSeriesNotAChat(bool firstModelIsChatOne, ModelComparisonVote vote, ModelComparisonModelChoice expected)
    {
        var task = DecidingTask(firstModelIsChatOne);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.Decide(vote, DECIDED, isBlind: true), Is.True);
            Assert.That(task.Vote, Is.EqualTo(expected));
            Assert.That(task.State, Is.EqualTo(ModelComparisonSeriesTaskState.DONE));
            Assert.That(task.FinishedAtUtc, Is.EqualTo(DECIDED));
        }
    }

    [Test]
    public void ADecisionAfterTheResultWasShownIsNotBlind()
    {
        var task = DecidingTask(firstModelIsChatOne: true);

        task.Decide(ModelComparisonVote.COLUMN_A, DECIDED, isBlind: false);

        Assert.That(task.IsBlind, Is.False);
    }

    [Test]
    public void AStepOutOfOrderChangesNothing()
    {
        var task = ModelComparisonSeriesTask.Begin(BEGUN, firstModelIsChatOne: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.FinishSecondChat(SECOND_SENT, SECOND_DONE, 1), Is.False);
            Assert.That(task.Decide(ModelComparisonVote.COLUMN_A, DECIDED, isBlind: true), Is.False);
            Assert.That(task.State, Is.EqualTo(ModelComparisonSeriesTaskState.FIRST_CHAT));
            Assert.That(task.Vote, Is.Null);
            Assert.That(task.ChatTwo.FinishedAtUtc, Is.Null);
        }

        var decided = DecidingTask(firstModelIsChatOne: true);
        decided.Decide(ModelComparisonVote.COLUMN_B, DECIDED, isBlind: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decided.FinishFirstChat(FIRST_SENT, FIRST_DONE, 5, "other"), Is.False);
            Assert.That(decided.Decide(ModelComparisonVote.COLUMN_A, DECIDED.AddMinutes(1), isBlind: false), Is.False);
            Assert.That(decided.Vote, Is.EqualTo(ModelComparisonModelChoice.SECOND));
            Assert.That(decided.IsBlind, Is.True);
        }
    }

    private static ModelComparisonSeriesTask DecidingTask(bool firstModelIsChatOne)
    {
        var task = ModelComparisonSeriesTask.Begin(BEGUN, firstModelIsChatOne);
        task.FinishFirstChat(FIRST_SENT, FIRST_DONE, 1, "question");
        task.FinishSecondChat(SECOND_SENT, SECOND_DONE, 1);
        return task;
    }
}