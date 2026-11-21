namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How often a model won the runs which were decided, together with how sure that share is.
/// </summary>
/// <remarks>
/// Only decided runs count: a tie says that neither model is better, not that both are. The share comes with the
/// Wilson score interval at 95 %, which stays meaningful for a handful of runs and for shares near 0 or 1, unlike the
/// textbook interval. Only when the whole interval lies above one half is the model clearly better, and only when it
/// lies below one half clearly worse; everything else is not yet clear.
///
/// The interval treats every run as an independent test. The runs of one batch ask the same question, though, so they
/// are less independent than that, and the interval is narrower than it should be. That is why the statistics count
/// runs, but show how many comparisons they come from as well.
/// </remarks>
public sealed record ModelComparisonWinShare
{
    /// <summary>
    /// The quantile of the standard normal distribution for a two-sided interval of 95 %.
    /// </summary>
    private const double Z_95 = 1.959964;

    /// <summary>
    /// How many decided runs the model won.
    /// </summary>
    public required int Wins { get; init; }

    /// <summary>
    /// How many decided runs the model lost.
    /// </summary>
    public required int Losses { get; init; }

    /// <summary>
    /// How many runs were decided.
    /// </summary>
    public int Decided => this.Wins + this.Losses;

    /// <summary>
    /// The share of the decided runs which the model won, or null when no run was decided.
    /// </summary>
    public double? Share { get; private init; }

    /// <summary>
    /// The lower end of the interval, or null when no run was decided.
    /// </summary>
    public double? LowerBound { get; private init; }

    /// <summary>
    /// The upper end of the interval, or null when no run was decided.
    /// </summary>
    public double? UpperBound { get; private init; }

    /// <summary>
    /// Whether the decided runs say which model is better.
    /// </summary>
    public ModelComparisonSignificance Significance { get; private init; }

    /// <summary>
    /// Computes the share and its interval.
    /// </summary>
    /// <param name="wins">How many decided runs the model won.</param>
    /// <param name="losses">How many decided runs the model lost.</param>
    /// <returns>The share.</returns>
    public static ModelComparisonWinShare Of(int wins, int losses)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wins);
        ArgumentOutOfRangeException.ThrowIfNegative(losses);

        var decided = wins + losses;
        if (decided == 0)
            return new() { Wins = 0, Losses = 0, Significance = ModelComparisonSignificance.NOT_YET_CLEAR };

        var share = (double)wins / decided;
        var zSquared = Z_95 * Z_95;
        var denominator = 1 + zSquared / decided;
        var center = share + zSquared / (2.0 * decided);
        var margin = Z_95 * Math.Sqrt(share * (1 - share) / decided + zSquared / (4.0 * decided * decided));
        var lower = (center - margin) / denominator;
        var upper = (center + margin) / denominator;

        return new()
        {
            Wins = wins,
            Losses = losses,
            Share = share,
            LowerBound = lower,
            UpperBound = upper,
            Significance = lower > 0.5
                ? ModelComparisonSignificance.CLEARLY_BETTER
                : upper < 0.5
                    ? ModelComparisonSignificance.CLEARLY_WORSE
                    : ModelComparisonSignificance.NOT_YET_CLEAR,
        };
    }
}