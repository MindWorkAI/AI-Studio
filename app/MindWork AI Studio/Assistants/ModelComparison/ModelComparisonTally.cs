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
    /// The tally of no runs at all.
    /// </summary>
    public static readonly ModelComparisonTally NONE = new()
    {
        TotalRunCount = 0,
        FirstModelWins = 0,
        SecondModelWins = 0,
        Ties = 0,
    };

    /// <summary>
    /// Counts the choices of several runs.
    /// </summary>
    /// <param name="choices">The choice of each run, already turned from a column into a model; null for a run without a usable choice.</param>
    /// <returns>The tally.</returns>
    public static ModelComparisonTally Of(IEnumerable<ModelComparisonModelChoice?> choices) => choices.Aggregate(NONE, (tally, choice) => tally.Plus(choice));

    /// <summary>
    /// Counts one more run.
    /// </summary>
    /// <param name="choice">The choice of the run, already turned from a column into a model; null for a run without a usable choice.</param>
    /// <returns>The tally with the run.</returns>
    public ModelComparisonTally Plus(ModelComparisonModelChoice? choice) => this with
    {
        TotalRunCount = this.TotalRunCount + 1,
        FirstModelWins = this.FirstModelWins + (choice is ModelComparisonModelChoice.FIRST ? 1 : 0),
        SecondModelWins = this.SecondModelWins + (choice is ModelComparisonModelChoice.SECOND ? 1 : 0),
        Ties = this.Ties + (choice is ModelComparisonModelChoice.TIE ? 1 : 0),
    };
}