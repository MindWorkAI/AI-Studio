using AIStudio.Assistants.ModelComparison;
using AIStudio.Provider;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks what the statistics make of the stored comparisons.
/// </summary>
[TestFixture]
public sealed class ModelComparisonStatisticsTests
{
    private static readonly DateTimeOffset EARLIER = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LATER = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    // OPEN_AI comes before MISTRAL by ModelComparisonModelKey.CompareTo, so A is the first model of their pair:
    private static readonly ModelComparisonModelIdentity A = new() { LLMProvider = LLMProviders.OPEN_AI, ModelId = "model-a", Label = "Model A" };
    private static readonly ModelComparisonModelIdentity B = new() { LLMProvider = LLMProviders.MISTRAL, ModelId = "model-b", Label = "Model B" };
    private static readonly ModelComparisonModelIdentity JUDGE = new() { LLMProvider = LLMProviders.ANTHROPIC, ModelId = "judge", Label = "Judge" };

    [Test]
    public async Task NoStoredComparisonsGiveEmptyStatistics()
    {
        var statistics = await ComputeAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.ComparisonCount, Is.Zero);
            Assert.That(statistics.RunCount, Is.Zero);
            Assert.That(statistics.VoteRanking, Is.Empty);
            Assert.That(statistics.JudgeRanking, Is.Empty);
            Assert.That(statistics.Pairings, Is.Empty);
            Assert.That(statistics.JudgeAgreements, Is.Empty);
            Assert.That(statistics.Speeds, Is.Empty);
        }
    }

    [Test]
    public async Task TheVotesRankTheModels()
    {
        var statistics = await ComputeAsync(Record(A, B, LATER, judge: null,
            Run(ModelComparisonModelChoice.FIRST),
            Run(ModelComparisonModelChoice.FIRST),
            Run(ModelComparisonModelChoice.SECOND),
            Run(ModelComparisonModelChoice.TIE),
            Run(vote: null)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.ComparisonCount, Is.EqualTo(1));
            Assert.That(statistics.RunCount, Is.EqualTo(5));
            Assert.That(statistics.VoteRanking.Select(standing => standing.Model.Label), Is.EqualTo(new[] { "Model A", "Model B" }));
            Assert.That(statistics.VoteRanking[0].Wins, Is.EqualTo(2));
            Assert.That(statistics.VoteRanking[0].Losses, Is.EqualTo(1));
            Assert.That(statistics.VoteRanking[0].Ties, Is.EqualTo(1));
            Assert.That(statistics.VoteRanking[1].Wins, Is.EqualTo(1));
            Assert.That(statistics.VoteRanking[1].Losses, Is.EqualTo(2));
            Assert.That(statistics.JudgeRanking, Is.Empty, "No judge took part.");
        }
    }

    [Test]
    public async Task TheVerdictsOfTheJudgeRankTheModelsOnTheirOwn()
    {
        var statistics = await ComputeAsync(Record(A, B, LATER, JUDGE,
            Run(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.SECOND),
            Run(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.SECOND)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.VoteRanking[0].Model.Label, Is.EqualTo("Model A"));
            Assert.That(statistics.JudgeRanking[0].Model.Label, Is.EqualTo("Model B"));
            Assert.That(statistics.JudgeRanking[0].Wins, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task APairCountsTheSameWhicheverModelWasPickedFirst()
    {
        // Model A wins both runs, once as the first model and once as the second one:
        var statistics = await ComputeAsync(
            Record(A, B, EARLIER, judge: null, Run(ModelComparisonModelChoice.FIRST)),
            Record(B, A, LATER, judge: null, Run(ModelComparisonModelChoice.SECOND)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.Pairings, Has.Count.EqualTo(1));
            Assert.That(statistics.Pairings[0].First.Label, Is.EqualTo("Model A"));
            Assert.That(statistics.Pairings[0].Second.Label, Is.EqualTo("Model B"));
            Assert.That(statistics.Pairings[0].ComparisonCount, Is.EqualTo(2));
            Assert.That(statistics.Pairings[0].Votes.FirstModelWins, Is.EqualTo(2));
            Assert.That(statistics.Pairings[0].Votes.SecondModelWins, Is.Zero);
            Assert.That(statistics.Pairings[0].WinShareOfFirstByVotes.Share, Is.EqualTo(1.0));
        }
    }

    [Test]
    public async Task AComparisonOfAModelWithItselfRanksNothing()
    {
        var statistics = await ComputeAsync(Record(A, A with { Label = "Model A again" }, LATER, judge: null,
            Run(ModelComparisonModelChoice.FIRST, firstTotalSeconds: 2, secondTotalSeconds: 4)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.VoteRanking, Is.Empty);
            Assert.That(statistics.Pairings, Is.Empty);
            Assert.That(statistics.Speeds, Has.Count.EqualTo(1), "Both answers came from the same model.");
            Assert.That(statistics.Speeds[0].AnswerCount, Is.EqualTo(2));
            Assert.That(statistics.Speeds[0].MedianTotalTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
        }
    }

    [Test]
    public async Task AJudgeAgreesWhenItChoosesTheSameAsTheUser()
    {
        var statistics = await ComputeAsync(Record(A, B, LATER, JUDGE,
            Run(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.FIRST),
            Run(ModelComparisonModelChoice.TIE, ModelComparisonModelChoice.FIRST),
            Run(ModelComparisonModelChoice.TIE, ModelComparisonModelChoice.TIE),
            Run(vote: null, ModelComparisonModelChoice.SECOND)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.JudgeAgreements, Has.Count.EqualTo(1));
            Assert.That(statistics.JudgeAgreements[0].Judge.Label, Is.EqualTo("Judge"));
            Assert.That(statistics.JudgeAgreements[0].Agreements, Is.EqualTo(2));
            Assert.That(statistics.JudgeAgreements[0].ComparedRuns, Is.EqualTo(3), "A run without a vote has nothing to agree with.");
            Assert.That(statistics.JudgeAgreements[0].Share, Is.EqualTo(2.0 / 3).Within(0.0001));
        }
    }

    [Test]
    public async Task TheSpeedIsTheMedianOfTheCompleteAnswers()
    {
        var statistics = await ComputeAsync(Record(A, B, LATER, judge: null,
            Run(ModelComparisonModelChoice.FIRST, firstTotalSeconds: 1, secondTotalSeconds: 20),
            Run(ModelComparisonModelChoice.FIRST, firstTotalSeconds: 3, secondTotalSeconds: null),
            Run(ModelComparisonModelChoice.FIRST, firstTotalSeconds: 60, secondTotalSeconds: 30)));

        var speedOfA = statistics.Speeds.Single(speed => speed.Model.Label == "Model A");
        var speedOfB = statistics.Speeds.Single(speed => speed.Model.Label == "Model B");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.Speeds[0].Model.Label, Is.EqualTo("Model A"), "The fastest model comes first.");
            Assert.That(speedOfA.AnswerCount, Is.EqualTo(3));
            Assert.That(speedOfA.MedianTotalTime, Is.EqualTo(TimeSpan.FromSeconds(3)), "One slow answer does not shift the median.");
            Assert.That(speedOfB.AnswerCount, Is.EqualTo(2), "An answer which never came does not count.");
            Assert.That(speedOfB.MedianTotalTime, Is.EqualTo(TimeSpan.FromSeconds(25)));
        }
    }

    [Test]
    public async Task AModelIsShownWithTheNameOfItsLatestComparison()
    {
        // The later comparison comes first, as the files may come in any order:
        var statistics = await ComputeAsync(
            Record(A with { Label = "Renamed model A" }, B, LATER, judge: null, Run(ModelComparisonModelChoice.FIRST)),
            Record(A, B, EARLIER, judge: null, Run(ModelComparisonModelChoice.FIRST)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.VoteRanking[0].Model.Label, Is.EqualTo("Renamed model A"));
            Assert.That(statistics.VoteRanking[0].Wins, Is.EqualTo(2), "Both comparisons count for the same model.");
        }
    }

    [Test]
    public async Task OnlyTheBlindTasksOfASeriesWithAShownResultCount()
    {
        var resolved = Series(A, B, JUDGE, isResolved: true,
            SeriesTask(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.FIRST),
            SeriesTask(ModelComparisonModelChoice.FIRST, ModelComparisonModelChoice.SECOND),
            SeriesTask(ModelComparisonModelChoice.SECOND, isBlind: false),
            SeriesTask(vote: null, state: ModelComparisonSeriesTaskState.DECIDING));
        var unresolved = Series(A, B, judge: null, isResolved: false, SeriesTask(ModelComparisonModelChoice.SECOND));

        var statistics = await ModelComparisonStatistics.ComputeAsync(ToAsync([]), [resolved, unresolved], ModelComparisonResultSource.ALL);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statistics.SeriesCount, Is.EqualTo(1));
            Assert.That(statistics.TaskCount, Is.EqualTo(2));
            Assert.That(statistics.IsEmpty, Is.False);
            Assert.That(statistics.Pairings.Single().Votes.FirstModelWins, Is.EqualTo(2));
            Assert.That(statistics.Pairings.Single().Votes.SecondModelWins, Is.Zero);
            Assert.That(statistics.Pairings.Single().ComparisonCount, Is.EqualTo(1));
            Assert.That(statistics.JudgeAgreements.Single().ComparedRuns, Is.EqualTo(2));
            Assert.That(statistics.JudgeAgreements.Single().Agreements, Is.EqualTo(1));
            Assert.That(statistics.Speeds, Is.Empty);
        }
    }

    [Test]
    public async Task TheSourceChoosesWhichResultsCount()
    {
        var record = Record(A, B, LATER, judge: null, Run(ModelComparisonModelChoice.FIRST));
        var series = Series(A, B, judge: null, isResolved: true, SeriesTask(ModelComparisonModelChoice.SECOND));

        var all = await ModelComparisonStatistics.ComputeAsync(ToAsync([record]), [series], ModelComparisonResultSource.ALL);
        var answers = await ModelComparisonStatistics.ComputeAsync(ToAsync([record]), [series], ModelComparisonResultSource.ANSWERS);
        var tasks = await ModelComparisonStatistics.ComputeAsync(ToAsync([record]), [series], ModelComparisonResultSource.TASKS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all.Pairings.Single().Votes.FirstModelWins, Is.EqualTo(1));
            Assert.That(all.Pairings.Single().Votes.SecondModelWins, Is.EqualTo(1));
            Assert.That(all.Pairings.Single().ComparisonCount, Is.EqualTo(2));

            Assert.That(answers.SeriesCount, Is.Zero);
            Assert.That(answers.Pairings.Single().Votes.SecondModelWins, Is.Zero);

            Assert.That(tasks.ComparisonCount, Is.Zero);
            Assert.That(tasks.Pairings.Single().Votes.FirstModelWins, Is.Zero);
            Assert.That(tasks.Pairings.Single().Votes.SecondModelWins, Is.EqualTo(1));
        }
    }

    private static Task<ModelComparisonStatistics> ComputeAsync(params ModelComparisonRecord[] records) => ModelComparisonStatistics.ComputeAsync(ToAsync(records));

    private static ModelComparisonSeries Series(ModelComparisonModelIdentity first, ModelComparisonModelIdentity second, ModelComparisonModelIdentity? judge, bool isResolved, params ModelComparisonSeriesTask[] tasks) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Series",
        CreatedAtUtc = EARLIER,
        First = first,
        Second = second,
        Judge = judge,
        IsResolved = isResolved,
        ResolvedAtUtc = isResolved ? LATER : null,
        Tasks = [.. tasks],
    };

    private static ModelComparisonSeriesTask SeriesTask(ModelComparisonModelChoice? vote, ModelComparisonModelChoice? judgeChoice = null, bool isBlind = true, ModelComparisonSeriesTaskState state = ModelComparisonSeriesTaskState.DONE) => new()
    {
        Id = Guid.NewGuid(),
        StartedAtUtc = EARLIER,
        State = state,
        Vote = vote,
        JudgeChoice = judgeChoice,
        IsBlind = isBlind,
    };

    private static async IAsyncEnumerable<ModelComparisonRecord> ToAsync(IEnumerable<ModelComparisonRecord> records)
    {
        foreach (var record in records)
        {
            await Task.Yield();
            yield return record;
        }
    }

    private static ModelComparisonRecord Record(ModelComparisonModelIdentity first, ModelComparisonModelIdentity second, DateTimeOffset finishedAtUtc, ModelComparisonModelIdentity? judge, params ModelComparisonRecordRun[] runs) => new()
    {
        Id = Guid.NewGuid(),
        FinishedAtUtc = finishedAtUtc,
        QuestionPreview = "Summarize it.",
        First = first,
        Second = second,
        Judge = judge,
        Runs = [.. runs],
    };

    private static ModelComparisonRecordRun Run(ModelComparisonModelChoice? vote, ModelComparisonModelChoice? judgeChoice = null, double? firstTotalSeconds = 1, double? secondTotalSeconds = 1) => new()
    {
        Vote = vote,
        JudgeChoice = judgeChoice,
        First = Answer(firstTotalSeconds),
        Second = Answer(secondTotalSeconds),
    };

    private static ModelComparisonRecordAnswer Answer(double? totalSeconds) => totalSeconds is { } seconds
        ? new ModelComparisonRecordAnswer { Answered = true, FirstTokenTime = TimeSpan.FromSeconds(seconds / 2), TotalTime = TimeSpan.FromSeconds(seconds) }
        : new ModelComparisonRecordAnswer { Answered = false };
}