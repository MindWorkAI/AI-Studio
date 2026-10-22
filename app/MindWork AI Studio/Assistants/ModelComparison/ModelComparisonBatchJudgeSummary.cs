namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How often the judge came out on each side across a batch of runs.
/// </summary>
/// <remarks>
/// Counted in First/Second terms, never in columns: the column a model stood in was drawn fresh for
/// every run, so only the model identity stays comparable across runs. A run whose judge did not
/// answer, or whose verdict could not be read, is left out of every count -- <see cref="ScoredRunCount"/>
/// says how many were actually counted, which is why it can be smaller than <see cref="TotalRunCount"/>.
/// </remarks>
public sealed record ModelComparisonBatchJudgeSummary
{
    public required int TotalRunCount { get; init; }

    public required int ScoredRunCount { get; init; }

    public required int FirstModelWins { get; init; }

    public required int SecondModelWins { get; init; }

    public required int Ties { get; init; }

    /// <summary>
    /// Tallies the judge's verdicts across a batch. Never throws: a run whose judge did not answer,
    /// or whose verdict could not be understood, is simply left out of the count.
    /// </summary>
    /// <param name="entries">The runs of the batch, in any order.</param>
    public static ModelComparisonBatchJudgeSummary From(IReadOnlyList<ModelComparisonBatchEntry> entries)
    {
        var firstModelWins = 0;
        var secondModelWins = 0;
        var ties = 0;

        foreach (var entry in entries)
        {
            if (entry.Result.Judge is not { Completed: true, Preferred: { } preferred })
                continue;

            switch (entry.PresentationOrder.ToModelChoice(preferred))
            {
                case ModelComparisonModelChoice.FIRST:
                    firstModelWins++;
                    break;

                case ModelComparisonModelChoice.SECOND:
                    secondModelWins++;
                    break;

                case ModelComparisonModelChoice.TIE:
                    ties++;
                    break;
            }
        }

        return new ModelComparisonBatchJudgeSummary
        {
            TotalRunCount = entries.Count,
            ScoredRunCount = firstModelWins + secondModelWins + ties,
            FirstModelWins = firstModelWins,
            SecondModelWins = secondModelWins,
            Ties = ties,
        };
    }
}
