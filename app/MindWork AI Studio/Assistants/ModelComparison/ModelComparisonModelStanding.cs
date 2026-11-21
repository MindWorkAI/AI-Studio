namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How a model did across all stored comparisons against any other model: by the votes of the user, or by the
/// verdicts of a judge.
/// </summary>
public sealed record ModelComparisonModelStanding
{
    /// <summary>
    /// The model, with the name it had in its latest comparison.
    /// </summary>
    public required ModelComparisonModelIdentity Model { get; init; }

    /// <summary>
    /// How many runs the model won.
    /// </summary>
    public int Wins { get; init; }

    /// <summary>
    /// How many runs the model lost.
    /// </summary>
    public int Losses { get; init; }

    /// <summary>
    /// How many runs ended in a tie.
    /// </summary>
    public int Ties { get; init; }

    /// <summary>
    /// The share of the decided runs which the model won, with how sure that share is.
    /// </summary>
    public ModelComparisonWinShare WinShare => ModelComparisonWinShare.Of(this.Wins, this.Losses);
}