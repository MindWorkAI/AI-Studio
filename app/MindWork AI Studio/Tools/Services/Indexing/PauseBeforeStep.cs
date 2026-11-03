namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// How long the indexing step which is about to start waits, cf. PauseBetweenSteps.GetPauseBeforeNextStep.
/// </summary>
/// <param name="Pause">The pause drawn for the gap since the last step.</param>
/// <param name="Remaining">What is left of the pause now. More than zero, unless this is NONE.</param>
internal sealed record PauseBeforeStep(TimeSpan Pause, TimeSpan Remaining)
{
    /// <summary>
    /// The step starts right away.
    /// </summary>
    public static readonly PauseBeforeStep NONE = new(TimeSpan.Zero, TimeSpan.Zero);
}