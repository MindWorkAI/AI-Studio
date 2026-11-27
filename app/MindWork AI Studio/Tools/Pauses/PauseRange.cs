namespace AIStudio.Tools.Pauses;

/// <summary>
/// The bounds of a random pause, e.g., between two indexing steps, cf. PauseBetweenSteps.
/// </summary>
/// <param name="MinimumSeconds">The shortest pause. Between the shortest limit of the feature and MaximumSeconds.</param>
/// <param name="MaximumSeconds">The longest pause, which may be drawn as well. At most the longest limit of the feature.</param>
public sealed record PauseRange(int MinimumSeconds, int MaximumSeconds)
{
    /// <summary>
    /// No pause at all, which is the default of the indexing.
    /// </summary>
    public static readonly PauseRange NONE = new(0, 0);

    /// <summary>
    /// Whether the next step starts right away.
    /// </summary>
    public bool IsNone => this.MaximumSeconds is 0;

    /// <summary>
    /// Works out the bounds which apply, so that the lower one never exceeds the upper one.
    /// </summary>
    /// <remarks>
    /// Both bounds are moved into the limits of the feature first. A bound which a configuration plugin
    /// locks wins, and the other one adjusts to it. A bound the plugin only presets counts like one the
    /// user set. Otherwise, the lower bound wins, i.e., the longer pause: it spares the server, which
    /// is what the pause is for. That holds as well when a plugin locks both bounds the wrong way round.<br/><br/>
    ///
    /// Which bound is locked matters only while the bounds are the wrong way round. Asking reads the
    /// managed state of the whole app, which bounds in order do without, so the locks are asked only then.
    /// </remarks>
    /// <param name="limits">The shortest and the longest pause the feature allows.</param>
    /// <param name="minimumSeconds">The lower bound as set.</param>
    /// <param name="isMinimumLocked">Whether a configuration plugin locks the lower bound.</param>
    /// <param name="maximumSeconds">The upper bound as set.</param>
    /// <param name="isMaximumLocked">Whether a configuration plugin locks the upper bound.</param>
    /// <returns>The bounds which apply.</returns>
    public static PauseRange GetEffective(PauseLimits limits, int minimumSeconds, Func<bool> isMinimumLocked, int maximumSeconds, Func<bool> isMaximumLocked)
    {
        var minimum = limits.Clamp(minimumSeconds);
        var maximum = limits.Clamp(maximumSeconds);
        if (minimum <= maximum)
            return new(minimum, maximum);

        if (isMaximumLocked() && !isMinimumLocked())
            return new(maximum, maximum);

        return new(minimum, minimum);
    }
}