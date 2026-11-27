namespace AIStudio.Tools.Pauses;

/// <summary>
/// The shortest and the longest pause a feature allows for either bound of its random pause, cf. PauseRange.
/// </summary>
/// <remarks>
/// Each feature sets its own limits next to its settings: the indexing allows no pause at all, for
/// example, while the batch processing always pauses for a few seconds.
/// </remarks>
/// <param name="ShortestSeconds">The shortest pause either bound may ask for.</param>
/// <param name="LongestSeconds">The longest pause either bound may ask for.</param>
public sealed record PauseLimits(int ShortestSeconds, int LongestSeconds)
{
    /// <summary>
    /// Whether a bound lies within the limits, e.g., one which a configuration plugin sets.
    /// </summary>
    /// <param name="seconds">The bound.</param>
    /// <returns>True when the feature allows the bound.</returns>
    public bool Allows(int seconds) => seconds >= this.ShortestSeconds && seconds <= this.LongestSeconds;

    /// <summary>
    /// Moves a bound into the limits.
    /// </summary>
    /// <param name="seconds">The bound.</param>
    /// <returns>The bound, or the limit it went beyond.</returns>
    public int Clamp(int seconds) => Math.Clamp(seconds, this.ShortestSeconds, this.LongestSeconds);
}