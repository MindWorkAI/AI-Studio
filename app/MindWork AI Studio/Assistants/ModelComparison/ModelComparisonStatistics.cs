namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What all stored comparisons say about the models: which model wins, which wins against which, how often each judge
/// agrees with the user, and how fast each model answers.
/// </summary>
/// <remarks>
/// A comparison of a model with itself, which tests how consistently it answers, says nothing about which model is
/// better: it counts toward the speed and the agreement of the judge, but not toward the rankings and the pairs.
/// </remarks>
public sealed record ModelComparisonStatistics
{
    /// <summary>
    /// How many stored comparisons there are.
    /// </summary>
    public int ComparisonCount { get; init; }

    /// <summary>
    /// How many runs the stored comparisons hold.
    /// </summary>
    public int RunCount { get; init; }

    /// <summary>
    /// The models by the votes of the user, the one with the highest share of wins first.
    /// </summary>
    public IReadOnlyList<ModelComparisonModelStanding> VoteRanking { get; init; } = [];

    /// <summary>
    /// The models by the verdicts of the judges, the one with the highest share of wins first.
    /// </summary>
    public IReadOnlyList<ModelComparisonModelStanding> JudgeRanking { get; init; } = [];

    /// <summary>
    /// Every pair of models compared with each other, the one compared most often first.
    /// </summary>
    public IReadOnlyList<ModelComparisonPairing> Pairings { get; init; } = [];

    /// <summary>
    /// Every judge, the one with the most compared runs first.
    /// </summary>
    public IReadOnlyList<ModelComparisonJudgeAgreement> JudgeAgreements { get; init; } = [];

    /// <summary>
    /// Every compared model, the fastest to complete an answer first.
    /// </summary>
    public IReadOnlyList<ModelComparisonModelSpeed> Speeds { get; init; } = [];

    /// <summary>
    /// Computes the statistics, one stored comparison at a time.
    /// </summary>
    /// <param name="records">The stored comparisons, see ModelComparisonStore.ReadResultsAsync.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    public static async Task<ModelComparisonStatistics> ComputeAsync(IAsyncEnumerable<ModelComparisonRecord> records, CancellationToken token = default)
    {
        var builder = new ModelComparisonStatisticsBuilder();
        await foreach (var record in records.WithCancellation(token))
            builder.Add(record);

        return builder.Build();
    }
}