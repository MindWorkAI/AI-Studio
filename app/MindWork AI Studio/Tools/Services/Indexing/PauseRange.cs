using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// The bounds of the random pause between two indexing steps, cf. PauseBetweenSteps.
/// </summary>
/// <param name="MinimumSeconds">The shortest pause. Between zero and MaximumSeconds.</param>
/// <param name="MaximumSeconds">The longest pause, which may be drawn as well. At most DataDataSourceIndexing.MAX_PAUSE_BETWEEN_STEPS_SECONDS.</param>
internal sealed record PauseRange(int MinimumSeconds, int MaximumSeconds)
{
    /// <summary>
    /// No pause at all, which is the default.
    /// </summary>
    public static readonly PauseRange NONE = new(0, 0);

    /// <summary>
    /// Whether the next step starts right away.
    /// </summary>
    public bool IsNone => this.MaximumSeconds is 0;

    /// <summary>
    /// The bounds which apply now, as the user set them and as configuration plugins lock them.
    /// </summary>
    /// <param name="settings">The settings of the indexing.</param>
    /// <returns>The bounds which apply.</returns>
    public static PauseRange FromSettings(DataDataSourceIndexing settings)
    {
        var minimumSeconds = settings.MinimumPauseBetweenStepsSeconds;
        var maximumSeconds = settings.MaximumPauseBetweenStepsSeconds;

        //
        // Which bound a configuration plugin locks matters only while the bounds are the wrong way
        // round. Asking reads the managed state of the whole app, which bounds in order do without:
        //
        if (minimumSeconds <= maximumSeconds)
            return GetEffective(minimumSeconds, false, maximumSeconds, false);

        var isMinimumLocked = ManagedConfiguration.TryGet(x => x.App.DataSourceIndexing, x => x.MinimumPauseBetweenStepsSeconds, out var minimumMeta) && minimumMeta.IsLocked;
        var isMaximumLocked = ManagedConfiguration.TryGet(x => x.App.DataSourceIndexing, x => x.MaximumPauseBetweenStepsSeconds, out var maximumMeta) && maximumMeta.IsLocked;
        return GetEffective(minimumSeconds, isMinimumLocked, maximumSeconds, isMaximumLocked);
    }

    /// <summary>
    /// Works out the bounds which apply, so that the lower one never exceeds the upper one.
    /// </summary>
    /// <remarks>
    /// A bound which a configuration plugin locks wins, and the other one adjusts to it. A bound the
    /// plugin only presets counts like one the user set. Otherwise, the lower bound wins, i.e., the
    /// longer pause: it spares the embedding server, which is what the pause is for. That holds as
    /// well when a plugin locks both bounds the wrong way round.
    /// </remarks>
    /// <param name="minimumSeconds">The lower bound as set.</param>
    /// <param name="isMinimumLocked">Whether a configuration plugin locks the lower bound.</param>
    /// <param name="maximumSeconds">The upper bound as set.</param>
    /// <param name="isMaximumLocked">Whether a configuration plugin locks the upper bound.</param>
    /// <returns>The bounds which apply.</returns>
    public static PauseRange GetEffective(int minimumSeconds, bool isMinimumLocked, int maximumSeconds, bool isMaximumLocked)
    {
        var minimum = Math.Clamp(minimumSeconds, 0, DataDataSourceIndexing.MAX_PAUSE_BETWEEN_STEPS_SECONDS);
        var maximum = Math.Clamp(maximumSeconds, 0, DataDataSourceIndexing.MAX_PAUSE_BETWEEN_STEPS_SECONDS);
        if (isMaximumLocked && !isMinimumLocked)
            return new(Math.Min(minimum, maximum), maximum);

        return new(minimum, Math.Max(minimum, maximum));
    }
}