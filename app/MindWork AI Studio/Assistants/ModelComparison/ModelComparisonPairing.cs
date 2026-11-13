namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How two models did directly against each other across all stored comparisons of the two.
/// </summary>
/// <remarks>
/// The first and the second model are those of the pair, see ModelComparisonPairKey, not those of a single comparison:
/// a comparison in which the user picked them the other way round counts the same.
/// </remarks>
public sealed record ModelComparisonPairing
{
    /// <summary>
    /// The first model of the pair, with the name it had in its latest comparison.
    /// </summary>
    public required ModelComparisonModelIdentity First { get; init; }

    /// <summary>
    /// The second model of the pair, with the name it had in its latest comparison.
    /// </summary>
    public required ModelComparisonModelIdentity Second { get; init; }

    /// <summary>
    /// The votes of the user.
    /// </summary>
    public ModelComparisonTally Votes { get; init; } = ModelComparisonTally.NONE;

    /// <summary>
    /// The verdicts of the judges.
    /// </summary>
    public ModelComparisonTally JudgeVerdicts { get; init; } = ModelComparisonTally.NONE;

    /// <summary>
    /// How many comparisons of the two there are.
    /// </summary>
    public int ComparisonCount { get; init; }

    /// <summary>
    /// The share of the decided votes which the first model won, with how sure that share is.
    /// </summary>
    public ModelComparisonWinShare WinShareOfFirstByVotes => ModelComparisonWinShare.Of(this.Votes.FirstModelWins, this.Votes.SecondModelWins);
}