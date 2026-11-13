namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How often each model came out ahead across several runs: by the votes of the user, or by the verdicts of the judge.
/// </summary>
/// <remarks>
/// Counted in models, never in columns: the column a model stood in was drawn fresh for every run, so only the model
/// stays comparable across runs. A run without a usable choice, such as one the user did not vote on, or one whose
/// judge gave no usable verdict, counts toward TotalRunCount, but toward nothing else.
/// </remarks>
public sealed record ModelComparisonTally
{
    /// <summary>
    /// How many runs there were, counted or not.
    /// </summary>
    public required int TotalRunCount { get; init; }

    public required int FirstModelWins { get; init; }

    public required int SecondModelWins { get; init; }

    public required int Ties { get; init; }

    /// <summary>
    /// How many runs had a usable choice and were counted.
    /// </summary>
    public int ScoredRunCount => this.FirstModelWins + this.SecondModelWins + this.Ties;

    /// <summary>
    /// How many runs had no usable choice and were left out.
    /// </summary>
    public int UnscoredRunCount => this.TotalRunCount - this.ScoredRunCount;

    /// <summary>
    /// Counts the choices of several runs.
    /// </summary>
    /// <param name="choices">The choice of each run, already turned from a column into a model; null for a run without a usable choice.</param>
    /// <returns>The tally.</returns>
    public static ModelComparisonTally Of(IReadOnlyCollection<ModelComparisonModelChoice?> choices) => new()
    {
        TotalRunCount = choices.Count,
        FirstModelWins = choices.Count(choice => choice is ModelComparisonModelChoice.FIRST),
        SecondModelWins = choices.Count(choice => choice is ModelComparisonModelChoice.SECOND),
        Ties = choices.Count(choice => choice is ModelComparisonModelChoice.TIE),
    };
}