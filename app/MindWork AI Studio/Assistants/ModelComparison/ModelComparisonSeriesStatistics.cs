namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What a comparison series says about its two models, with their names, once the user asked for the result.
/// </summary>
/// <remarks>
/// The wins count only the blind tasks: a task decided after the result was shown is not blind, and is shown apart.
/// Whether the chat a model answered in mattered shows in the wins of the first and of the second chat: the user knows
/// the task better in the second chat, and when that chat wins much more often, the order influenced the decisions.
/// The effort counts every decided task, since the user does not choose it.
/// </remarks>
public sealed record ModelComparisonSeriesStatistics
{
    /// <summary>
    /// Both models of the series against each other, by the blind votes and the verdicts of the judge on those tasks.
    /// ComparisonCount counts the blind tasks.
    /// </summary>
    public required ModelComparisonPairing Pairing { get; init; }

    /// <summary>
    /// The votes on the tasks decided after the result was shown.
    /// </summary>
    public ModelComparisonTally NotBlindVotes { get; init; } = ModelComparisonTally.NONE;

    /// <summary>
    /// How many blind tasks the model in the first chat won.
    /// </summary>
    public int WinsOfChatOne { get; init; }

    /// <summary>
    /// How many blind tasks the model in the second chat won.
    /// </summary>
    public int WinsOfChatTwo { get; init; }

    /// <summary>
    /// How often the judge chose the same as the user on the blind tasks, or null without a judge.
    /// </summary>
    public ModelComparisonJudgeAgreement? JudgeAgreement { get; init; }

    /// <summary>
    /// The effort with the first model.
    /// </summary>
    public required ModelComparisonSeriesEffort FirstEffort { get; init; }

    /// <summary>
    /// The effort with the second model.
    /// </summary>
    public required ModelComparisonSeriesEffort SecondEffort { get; init; }

    /// <summary>
    /// Computes the statistics of a series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The statistics.</returns>
    public static ModelComparisonSeriesStatistics Of(ModelComparisonSeries series)
    {
        var decided = series.Tasks.Where(task => task.State is ModelComparisonSeriesTaskState.DONE).ToList();
        var blind = decided.Where(task => task.IsBlind).ToList();
        var votesWithWinner = blind.Where(task => task.Vote is ModelComparisonModelChoice.FIRST or ModelComparisonModelChoice.SECOND).ToList();
        var judged = blind.Where(task => task is { Vote: not null, JudgeChoice: not null }).ToList();

        return new()
        {
            Pairing = new()
            {
                First = series.First,
                Second = series.Second,
                ComparisonCount = blind.Count,
                Votes = ModelComparisonTally.Of(blind.Select(task => task.Vote)),
                JudgeVerdicts = ModelComparisonTally.Of(blind.Select(task => task.JudgeChoice)),
            },
            NotBlindVotes = ModelComparisonTally.Of(decided.Where(task => !task.IsBlind).Select(task => task.Vote)),
            WinsOfChatOne = votesWithWinner.Count(task => task.ChatNumberOf(task.Vote!.Value) == 1),
            WinsOfChatTwo = votesWithWinner.Count(task => task.ChatNumberOf(task.Vote!.Value) == 2),
            JudgeAgreement = series.Judge is { } judge
                ? new() { Judge = judge, ComparedRuns = judged.Count, Agreements = judged.Count(task => task.Vote == task.JudgeChoice) }
                : null,
            FirstEffort = EffortOf(series.First, decided.Select(task => task.ChatOfFirstModel)),
            SecondEffort = EffortOf(series.Second, decided.Select(task => task.ChatOfSecondModel)),
        };
    }

    private static ModelComparisonSeriesEffort EffortOf(ModelComparisonModelIdentity model, IEnumerable<ModelComparisonSeriesChat> chats)
    {
        var finishedChats = chats.Where(chat => chat.Duration is not null).ToList();
        if (finishedChats.Count == 0)
            return new() { Model = model };

        return new()
        {
            Model = model,
            TaskCount = finishedChats.Count,
            AverageUserMessages = finishedChats.Average(chat => chat.UserMessageCount),
            MedianDuration = ModelComparisonStatisticsBuilder.Median([.. finishedChats.Select(chat => chat.Duration!.Value)]),
        };
    }
}