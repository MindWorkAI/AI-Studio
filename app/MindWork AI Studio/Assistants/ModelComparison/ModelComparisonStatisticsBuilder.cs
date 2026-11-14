namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Folds stored comparisons into ModelComparisonStatistics, one at a time, so that never more than one of them is in
/// memory: only the counts and the times stay.
/// </summary>
internal sealed class ModelComparisonStatisticsBuilder
{
    /// <summary>
    /// The identity of each model from its latest comparison, so that a renamed provider entry shows its new name.
    /// </summary>
    private readonly Dictionary<ModelComparisonModelKey, ModelComparisonModelIdentity> latestIdentities = [];

    /// <summary>
    /// When each model took part in its latest comparison.
    /// </summary>
    private readonly Dictionary<ModelComparisonModelKey, DateTimeOffset> latestTimes = [];

    private readonly Dictionary<ModelComparisonModelKey, ModelComparisonModelStanding> voteStandings = [];

    private readonly Dictionary<ModelComparisonModelKey, ModelComparisonModelStanding> judgeStandings = [];

    private readonly Dictionary<ModelComparisonPairKey, ModelComparisonPairing> pairings = [];

    private readonly Dictionary<ModelComparisonModelKey, ModelComparisonJudgeAgreement> judgeAgreements = [];

    private readonly Dictionary<ModelComparisonModelKey, List<TimeSpan>> firstTokenTimes = [];

    private readonly Dictionary<ModelComparisonModelKey, List<TimeSpan>> totalTimes = [];

    private int comparisonCount;

    private int runCount;

    private int seriesCount;

    private int taskCount;

    /// <summary>
    /// Counts a stored comparison of the stability mode.
    /// </summary>
    /// <param name="record">The stored comparison.</param>
    public void Add(ModelComparisonRecord record)
    {
        this.comparisonCount++;
        this.runCount += record.Runs.Count;
        this.AddComparison(record.First, record.Second, record.Judge, record.FinishedAtUtc);

        foreach (var run in record.Runs)
        {
            this.AddTimes(record.First.Key, run.First);
            this.AddTimes(record.Second.Key, run.Second);
            this.AddDecision(record.First, record.Second, record.Judge, run.Vote, run.JudgeChoice);
        }
    }

    /// <summary>
    /// Counts the blind tasks of a comparison series whose result the user saw. A series before its result counts not
    /// at all: its models must not show up anywhere with their names. A task decided after the result is not blind and
    /// stays out, too. The tasks have no times of the answers, so they count toward neither speed.
    /// </summary>
    /// <param name="series">The series.</param>
    public void Add(ModelComparisonSeries series)
    {
        if (!series.IsResolved)
            return;

        var blindTasks = series.Tasks.Where(task => task is { State: ModelComparisonSeriesTaskState.DONE, IsBlind: true }).ToList();
        if (blindTasks.Count == 0)
            return;

        this.seriesCount++;
        this.taskCount += blindTasks.Count;
        this.AddComparison(series.First, series.Second, series.Judge, series.ResolvedAtUtc ?? series.LastActivityUtc);

        foreach (var task in blindTasks)
            this.AddDecision(series.First, series.Second, series.Judge, task.Vote, task.JudgeChoice);
    }

    /// <summary>
    /// Gets the statistics of everything counted so far.
    /// </summary>
    /// <returns>The statistics.</returns>
    public ModelComparisonStatistics Build() => new()
    {
        ComparisonCount = this.comparisonCount,
        RunCount = this.runCount,
        SeriesCount = this.seriesCount,
        TaskCount = this.taskCount,
        VoteRanking = this.Rank(this.voteStandings),
        JudgeRanking = this.Rank(this.judgeStandings),
        Pairings =
        [
            .. this.pairings.Values
                .Select(pairing => pairing with { First = this.latestIdentities[pairing.First.Key], Second = this.latestIdentities[pairing.Second.Key] })
                .OrderByDescending(pairing => pairing.ComparisonCount)
                .ThenBy(pairing => pairing.First.Label, StringComparer.CurrentCultureIgnoreCase),
        ],
        JudgeAgreements =
        [
            .. this.judgeAgreements
                .Select(agreement => agreement.Value with { Judge = this.latestIdentities[agreement.Key] })
                .OrderByDescending(agreement => agreement.ComparedRuns)
                .ThenBy(agreement => agreement.Judge.Label, StringComparer.CurrentCultureIgnoreCase),
        ],
        Speeds =
        [
            .. this.totalTimes.Keys
                .Select(key => new ModelComparisonModelSpeed
                {
                    Model = this.latestIdentities[key],
                    AnswerCount = this.totalTimes[key].Count,
                    MedianFirstTokenTime = Median(this.firstTokenTimes[key]),
                    MedianTotalTime = Median(this.totalTimes[key]),
                })
                .OrderBy(speed => speed.MedianTotalTime ?? TimeSpan.MaxValue)
                .ThenBy(speed => speed.Model.Label, StringComparer.CurrentCultureIgnoreCase),
        ],
    };

    /// <summary>
    /// Counts a comparison of two models: remembers their names, and counts it for their pair.
    /// </summary>
    /// <param name="first">The first model of the comparison.</param>
    /// <param name="second">The second model of the comparison.</param>
    /// <param name="judge">The judge, or null without one.</param>
    /// <param name="finishedAtUtc">When the comparison was finished.</param>
    private void AddComparison(ModelComparisonModelIdentity first, ModelComparisonModelIdentity second, ModelComparisonModelIdentity? judge, DateTimeOffset finishedAtUtc)
    {
        this.Remember(first, finishedAtUtc);
        this.Remember(second, finishedAtUtc);
        if (judge is not null)
            this.Remember(judge, finishedAtUtc);

        // A comparison of a model with itself tests how consistently it answers, not which model is better:
        if (first.Key == second.Key)
            return;

        var pair = ModelComparisonPairKey.Of(first.Key, second.Key);
        var pairIsSwapped = pair.First != first.Key;
        var pairing = this.pairings.GetValueOrDefault(pair) ?? new ModelComparisonPairing
        {
            First = pairIsSwapped ? second : first,
            Second = pairIsSwapped ? first : second,
        };

        this.pairings[pair] = pairing with { ComparisonCount = pairing.ComparisonCount + 1 };
    }

    /// <summary>
    /// Counts the vote of the user and the choice of the judge on one run or task.
    /// </summary>
    /// <param name="first">The first model of the comparison.</param>
    /// <param name="second">The second model of the comparison.</param>
    /// <param name="judge">The judge, or null without one.</param>
    /// <param name="vote">The vote of the user, or null without one.</param>
    /// <param name="judgeChoice">The choice of the judge, or null without a usable verdict.</param>
    private void AddDecision(ModelComparisonModelIdentity first, ModelComparisonModelIdentity second, ModelComparisonModelIdentity? judge, ModelComparisonModelChoice? vote, ModelComparisonModelChoice? judgeChoice)
    {
        if (first.Key != second.Key)
        {
            Count(this.voteStandings, first, second, vote);
            Count(this.judgeStandings, first, second, judgeChoice);

            var pair = ModelComparisonPairKey.Of(first.Key, second.Key);
            var pairIsSwapped = pair.First != first.Key;
            var pairing = this.pairings[pair];
            this.pairings[pair] = pairing with
            {
                Votes = pairing.Votes.Plus(Oriented(vote, pairIsSwapped)),
                JudgeVerdicts = pairing.JudgeVerdicts.Plus(Oriented(judgeChoice, pairIsSwapped)),
            };
        }

        if (judge is not null && vote is { } decidedVote && judgeChoice is { } decidedJudgeChoice)
        {
            var agreement = this.judgeAgreements.GetValueOrDefault(judge.Key) ?? new ModelComparisonJudgeAgreement { Judge = judge };
            this.judgeAgreements[judge.Key] = agreement with
            {
                Agreements = agreement.Agreements + (decidedVote == decidedJudgeChoice ? 1 : 0),
                ComparedRuns = agreement.ComparedRuns + 1,
            };
        }
    }

    /// <summary>
    /// Remembers the identity of a model when its comparison is the latest one so far.
    /// </summary>
    /// <param name="identity">The identity in the stored comparison.</param>
    /// <param name="finishedAtUtc">When the comparison was finished.</param>
    private void Remember(ModelComparisonModelIdentity identity, DateTimeOffset finishedAtUtc)
    {
        var key = identity.Key;
        if (this.latestTimes.TryGetValue(key, out var latest) && finishedAtUtc < latest)
            return;

        this.latestTimes[key] = finishedAtUtc;
        this.latestIdentities[key] = identity;
    }

    /// <summary>
    /// Keeps the times of a complete answer.
    /// </summary>
    /// <param name="model">The key of the model.</param>
    /// <param name="answer">How the model did in the run.</param>
    private void AddTimes(ModelComparisonModelKey model, ModelComparisonRecordAnswer answer)
    {
        if (!this.totalTimes.TryGetValue(model, out var totals))
        {
            totals = [];
            this.totalTimes[model] = totals;
            this.firstTokenTimes[model] = [];
        }

        // Only complete answers count: a failure or a cancellation would make a model look faster than it is.
        if (!answer.Answered || answer.TotalTime is not { } totalTime)
            return;

        totals.Add(totalTime);
        if (answer.FirstTokenTime is { } firstTokenTime)
            this.firstTokenTimes[model].Add(firstTokenTime);
    }

    /// <summary>
    /// Ranks the models, the one with the highest share of wins first; a model without decided runs comes last.
    /// </summary>
    /// <param name="standings">The standings by model.</param>
    /// <returns>The ranking, with the latest name of each model.</returns>
    private IReadOnlyList<ModelComparisonModelStanding> Rank(Dictionary<ModelComparisonModelKey, ModelComparisonModelStanding> standings) =>
    [
        .. standings
            .Select(standing => standing.Value with { Model = this.latestIdentities[standing.Key] })
            .OrderByDescending(standing => standing.WinShare.Share ?? -1)
            .ThenByDescending(standing => standing.WinShare.Decided)
            .ThenBy(standing => standing.Model.Label, StringComparer.CurrentCultureIgnoreCase),
    ];

    /// <summary>
    /// Counts a choice for and against both models of a comparison.
    /// </summary>
    /// <param name="standings">The standings by model.</param>
    /// <param name="first">The first model of the comparison.</param>
    /// <param name="second">The second model of the comparison.</param>
    /// <param name="choice">The choice of the run, or null without one.</param>
    private static void Count(Dictionary<ModelComparisonModelKey, ModelComparisonModelStanding> standings, ModelComparisonModelIdentity first, ModelComparisonModelIdentity second, ModelComparisonModelChoice? choice)
    {
        switch (choice)
        {
            case ModelComparisonModelChoice.FIRST:
                Change(standings, first, standing => standing with { Wins = standing.Wins + 1 });
                Change(standings, second, standing => standing with { Losses = standing.Losses + 1 });
                break;

            case ModelComparisonModelChoice.SECOND:
                Change(standings, first, standing => standing with { Losses = standing.Losses + 1 });
                Change(standings, second, standing => standing with { Wins = standing.Wins + 1 });
                break;

            case ModelComparisonModelChoice.TIE:
                Change(standings, first, standing => standing with { Ties = standing.Ties + 1 });
                Change(standings, second, standing => standing with { Ties = standing.Ties + 1 });
                break;
        }
    }

    /// <summary>
    /// Changes the standing of a model.
    /// </summary>
    /// <param name="standings">The standings by model.</param>
    /// <param name="model">The model.</param>
    /// <param name="change">The change.</param>
    private static void Change(Dictionary<ModelComparisonModelKey, ModelComparisonModelStanding> standings, ModelComparisonModelIdentity model, Func<ModelComparisonModelStanding, ModelComparisonModelStanding> change)
    {
        var standing = standings.GetValueOrDefault(model.Key) ?? new ModelComparisonModelStanding { Model = model };
        standings[model.Key] = change(standing);
    }

    /// <summary>
    /// Turns a choice in the order of a comparison into the order of its pair.
    /// </summary>
    /// <param name="choice">The choice, in the order of the comparison.</param>
    /// <param name="pairIsSwapped">Whether the pair names the models the other way round.</param>
    private static ModelComparisonModelChoice? Oriented(ModelComparisonModelChoice? choice, bool pairIsSwapped) => !pairIsSwapped ? choice : choice switch
    {
        ModelComparisonModelChoice.FIRST => ModelComparisonModelChoice.SECOND,
        ModelComparisonModelChoice.SECOND => ModelComparisonModelChoice.FIRST,

        _ => choice,
    };

    /// <summary>
    /// Gets the median of some times.
    /// </summary>
    /// <param name="times">The times.</param>
    /// <returns>The median, or null without any times.</returns>
    internal static TimeSpan? Median(IReadOnlyCollection<TimeSpan> times)
    {
        if (times.Count == 0)
            return null;

        var sorted = times.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}