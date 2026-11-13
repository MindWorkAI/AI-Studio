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

    /// <summary>
    /// Counts a stored comparison.
    /// </summary>
    /// <param name="record">The stored comparison.</param>
    public void Add(ModelComparisonRecord record)
    {
        this.comparisonCount++;
        this.runCount += record.Runs.Count;

        var first = this.Remember(record.First, record.FinishedAtUtc);
        var second = this.Remember(record.Second, record.FinishedAtUtc);
        if (record.Judge is { } judge)
            this.Remember(judge, record.FinishedAtUtc);

        var comparesTwoModels = first != second;
        var pair = ModelComparisonPairKey.Of(first, second);
        var pairIsSwapped = pair.First != first;
        if (comparesTwoModels)
        {
            var pairing = this.pairings.GetValueOrDefault(pair) ?? new ModelComparisonPairing
            {
                First = pairIsSwapped ? record.Second : record.First,
                Second = pairIsSwapped ? record.First : record.Second,
            };

            this.pairings[pair] = pairing with { ComparisonCount = pairing.ComparisonCount + 1 };
        }

        foreach (var run in record.Runs)
        {
            this.AddTimes(first, run.First);
            this.AddTimes(second, run.Second);

            if (comparesTwoModels)
            {
                Count(this.voteStandings, record.First, record.Second, run.Vote);
                Count(this.judgeStandings, record.First, record.Second, run.JudgeChoice);

                var pairing = this.pairings[pair];
                this.pairings[pair] = pairing with
                {
                    Votes = pairing.Votes.Plus(Oriented(run.Vote, pairIsSwapped)),
                    JudgeVerdicts = pairing.JudgeVerdicts.Plus(Oriented(run.JudgeChoice, pairIsSwapped)),
                };
            }

            if (record.Judge is { } judgeOfRun && run.Vote is { } vote && run.JudgeChoice is { } judgeChoice)
            {
                var agreement = this.judgeAgreements.GetValueOrDefault(judgeOfRun.Key) ?? new ModelComparisonJudgeAgreement { Judge = judgeOfRun };
                this.judgeAgreements[judgeOfRun.Key] = agreement with
                {
                    Agreements = agreement.Agreements + (vote == judgeChoice ? 1 : 0),
                    ComparedRuns = agreement.ComparedRuns + 1,
                };
            }
        }
    }

    /// <summary>
    /// Gets the statistics of everything counted so far.
    /// </summary>
    /// <returns>The statistics.</returns>
    public ModelComparisonStatistics Build() => new()
    {
        ComparisonCount = this.comparisonCount,
        RunCount = this.runCount,
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
    /// Remembers the identity of a model when its comparison is the latest one so far.
    /// </summary>
    /// <param name="identity">The identity in the stored comparison.</param>
    /// <param name="finishedAtUtc">When the comparison was finished.</param>
    /// <returns>The key of the model.</returns>
    private ModelComparisonModelKey Remember(ModelComparisonModelIdentity identity, DateTimeOffset finishedAtUtc)
    {
        var key = identity.Key;
        if (!this.latestTimes.TryGetValue(key, out var latest) || finishedAtUtc >= latest)
        {
            this.latestTimes[key] = finishedAtUtc;
            this.latestIdentities[key] = identity;
        }

        return key;
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
    private static TimeSpan? Median(List<TimeSpan> times)
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