namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How often the user's own vote came out on each side across a batch of runs.
/// </summary>
/// <remarks>
/// Counted in First/Second terms, never in columns, for the same reason as
/// <see cref="ModelComparisonBatchJudgeSummary"/>: the column a model stood in was drawn fresh for
/// every run. A run the user never voted on -- the batch was left before reaching it -- is left out
/// of every count, the same way an unanswered judge is left out of the judge tally.
/// </remarks>
public sealed record ModelComparisonBatchVoteSummary
{
    public required int TotalRunCount { get; init; }

    public required int ScoredRunCount { get; init; }

    public required int FirstModelWins { get; init; }

    public required int SecondModelWins { get; init; }

    public required int Ties { get; init; }

    /// <summary>
    /// Tallies the user's own votes across a batch, alongside its entries. Never throws: a run with
    /// no vote at its index is simply left out of the count.
    /// </summary>
    /// <param name="entries">The runs of the batch, each with its own presentation order.</param>
    /// <param name="votes">The user's vote for each entry, at the same index. Null means not voted on.</param>
    public static ModelComparisonBatchVoteSummary From(IReadOnlyList<ModelComparisonBatchEntry> entries, IReadOnlyList<ModelComparisonVote?> votes)
    {
        var firstModelWins = 0;
        var secondModelWins = 0;
        var ties = 0;

        for (var index = 0; index < entries.Count; index++)
        {
            if (index >= votes.Count || votes[index] is not { } vote)
                continue;

            switch (entries[index].PresentationOrder.ToModelChoice(vote))
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

        return new ModelComparisonBatchVoteSummary
        {
            TotalRunCount = entries.Count,
            ScoredRunCount = firstModelWins + secondModelWins + ties,
            FirstModelWins = firstModelWins,
            SecondModelWins = secondModelWins,
            Ties = ties,
        };
    }
}
