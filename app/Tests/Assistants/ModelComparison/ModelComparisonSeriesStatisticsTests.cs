using AIStudio.Assistants.ModelComparison;
using AIStudio.Provider;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks what the result of a comparison series says about its two models.
/// </summary>
[TestFixture]
public sealed class ModelComparisonSeriesStatisticsTests
{
    private static readonly DateTimeOffset START = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private static readonly ModelComparisonModelIdentity A = new() { LLMProvider = LLMProviders.OPEN_AI, ModelId = "model-a", Label = "Model A" };
    private static readonly ModelComparisonModelIdentity B = new() { LLMProvider = LLMProviders.MISTRAL, ModelId = "model-b", Label = "Model B" };
    private static readonly ModelComparisonModelIdentity JUDGE = new() { LLMProvider = LLMProviders.ANTHROPIC, ModelId = "judge", Label = "Judge" };

    [Test]
    public void TheBlindTasksDecideAndTheOthersStandApart()
    {
        var statistics = ModelComparisonSeriesStatistics.Of(Series(
            DecidedTask(firstModelIsChatOne: true, ModelComparisonVote.COLUMN_A),
            DecidedTask(firstModelIsChatOne: false, ModelComparisonVote.COLUMN_B),
            DecidedTask(firstModelIsChatOne: true, ModelComparisonVote.TIE),
            DecidedTask(firstModelIsChatOne: true, ModelComparisonVote.COLUMN_B, isBlind: false),
            ModelComparisonSeriesTask.Begin(START, firstModelIsChatOne: true)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.Pairing.First, Is.EqualTo(A));
            Assert.That(statistics.Pairing.Second, Is.EqualTo(B));
            Assert.That(statistics.Pairing.ComparisonCount, Is.EqualTo(3));
            Assert.That(statistics.Pairing.Votes.FirstModelWins, Is.EqualTo(2));
            Assert.That(statistics.Pairing.Votes.SecondModelWins, Is.Zero);
            Assert.That(statistics.Pairing.Votes.Ties, Is.EqualTo(1));
            Assert.That(statistics.NotBlindVotes.TotalRunCount, Is.EqualTo(1));
            Assert.That(statistics.NotBlindVotes.SecondModelWins, Is.EqualTo(1));
        }
    }

    [Test]
    public void TheWinsOfTheFirstAndTheSecondChatShowWhetherTheOrderMattered()
    {
        var statistics = ModelComparisonSeriesStatistics.Of(Series(
            DecidedTask(firstModelIsChatOne: true, ModelComparisonVote.COLUMN_B),
            DecidedTask(firstModelIsChatOne: false, ModelComparisonVote.COLUMN_B),
            DecidedTask(firstModelIsChatOne: false, ModelComparisonVote.COLUMN_A),
            DecidedTask(firstModelIsChatOne: true, ModelComparisonVote.TIE)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.WinsOfChatOne, Is.EqualTo(1));
            Assert.That(statistics.WinsOfChatTwo, Is.EqualTo(2));
        }
    }

    [Test]
    public void TheJudgeAgreesWhenItChoseTheSameModel()
    {
        var agreeing = DecidedTask(firstModelIsChatOne: true, ModelComparisonVote.COLUMN_A);
        agreeing.TakeVerdict(new ModelComparisonJudgeVerdict { Label = "Judge", Completed = true, Preferred = ModelComparisonVote.COLUMN_A });

        var disagreeing = DecidedTask(firstModelIsChatOne: false, ModelComparisonVote.COLUMN_A);
        disagreeing.TakeVerdict(new ModelComparisonJudgeVerdict { Label = "Judge", Completed = true, Preferred = ModelComparisonVote.TIE });

        var withoutVerdict = DecidedTask(firstModelIsChatOne: true, ModelComparisonVote.COLUMN_B);

        var statistics = ModelComparisonSeriesStatistics.Of(Series(agreeing, disagreeing, withoutVerdict));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.JudgeAgreement, Is.Not.Null);
            Assert.That(statistics.JudgeAgreement!.ComparedRuns, Is.EqualTo(2));
            Assert.That(statistics.JudgeAgreement.Agreements, Is.EqualTo(1));
            Assert.That(statistics.Pairing.JudgeVerdicts.FirstModelWins, Is.EqualTo(1));
            Assert.That(statistics.Pairing.JudgeVerdicts.Ties, Is.EqualTo(1));
        }
    }

    [Test]
    public void TheEffortCountsMessagesAndTheMedianTimeOfEachModel()
    {
        var statistics = ModelComparisonSeriesStatistics.Of(Series(
            DecidedTask(firstModelIsChatOne: true, ModelComparisonVote.COLUMN_A, chatOneMinutes: 10, chatOneMessages: 3, chatTwoMinutes: 4, chatTwoMessages: 1),
            DecidedTask(firstModelIsChatOne: false, ModelComparisonVote.COLUMN_A, chatOneMinutes: 6, chatOneMessages: 2, chatTwoMinutes: 2, chatTwoMessages: 1),
            DecidedTask(firstModelIsChatOne: true, ModelComparisonVote.COLUMN_A, chatOneMinutes: 600, chatOneMessages: 1, chatTwoMinutes: 5, chatTwoMessages: 4)));

        using (Assert.EnterMultipleScope())
        {
            // The first model answered in the first chat of the first and the third task, and in the second chat of the second:
            Assert.That(statistics.FirstEffort.Model, Is.EqualTo(A));
            Assert.That(statistics.FirstEffort.TaskCount, Is.EqualTo(3));
            Assert.That(statistics.FirstEffort.AverageUserMessages, Is.EqualTo(2.0 / 3 + 1).Within(0.001));
            Assert.That(statistics.FirstEffort.MedianDuration, Is.EqualTo(TimeSpan.FromMinutes(10)));

            Assert.That(statistics.SecondEffort.Model, Is.EqualTo(B));
            Assert.That(statistics.SecondEffort.AverageUserMessages, Is.EqualTo(7.0 / 3).Within(0.001));
            Assert.That(statistics.SecondEffort.MedianDuration, Is.EqualTo(TimeSpan.FromMinutes(5)));
        }
    }

    [Test]
    public void ASeriesWithoutJudgeAndWithoutTasksHasNothingToShow()
    {
        var series = Series() with { Judge = null };
        var statistics = ModelComparisonSeriesStatistics.Of(series);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.Pairing.ComparisonCount, Is.Zero);
            Assert.That(statistics.JudgeAgreement, Is.Null);
            Assert.That(statistics.FirstEffort.TaskCount, Is.Zero);
            Assert.That(statistics.FirstEffort.AverageUserMessages, Is.Null);
            Assert.That(statistics.FirstEffort.MedianDuration, Is.Null);
        }
    }

    private static ModelComparisonSeries Series(params ModelComparisonSeriesTask[] tasks) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Series",
        CreatedAtUtc = START,
        First = A,
        Second = B,
        Judge = JUDGE,
        Tasks = [.. tasks],
    };

    private static ModelComparisonSeriesTask DecidedTask(bool firstModelIsChatOne, ModelComparisonVote vote, bool isBlind = true, double chatOneMinutes = 1, int chatOneMessages = 1, double chatTwoMinutes = 1, int chatTwoMessages = 1)
    {
        var task = ModelComparisonSeriesTask.Begin(START, firstModelIsChatOne);
        var chatOneDone = START.AddMinutes(chatOneMinutes);
        task.FinishFirstChat(START, chatOneDone, chatOneMessages, "question");
        task.FinishSecondChat(chatOneDone, chatOneDone.AddMinutes(chatTwoMinutes), chatTwoMessages);
        task.Decide(vote, chatOneDone.AddMinutes(chatTwoMinutes + 1), isBlind);
        return task;
    }
}