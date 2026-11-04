namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// The pause between two indexing steps, which spares an embedding server that many people share.
/// </summary>
/// <remarks>
/// Not to be confused with the pauses of the indexing as a whole, e.g., while a backup copies the
/// indexes, cf. IndexingPauses.<br/><br/>
///
/// The pause is a gap between the end of one step and the start of the next, whichever data source
/// either of them belongs to: it is about the load on the embedding server, not about the order of
/// the data sources. Only a step which worked on documents opens a gap. Its pause is drawn once the
/// next step is about to start, from the bounds which apply then, and the time which passed in the
/// meantime counts toward it. When no step comes, nothing is drawn.
/// </remarks>
internal sealed class PauseBetweenSteps
{
    private readonly Lock stateLock = new();

    /// <summary>
    /// When the last step which worked on documents ended. Null before the first one.
    /// </summary>
    private DateTimeOffset? lastStepEndedUtc;

    /// <summary>
    /// The pause drawn for the gap since the last step. Null until a step is about to start.
    /// </summary>
    private TimeSpan? drawnPause;

    /// <summary>
    /// Notes that a step which worked on documents ended, which opens a new gap.
    /// </summary>
    /// <param name="nowUtc">The current time, passed in so the pause can be tested.</param>
    public void RecordStepEnded(DateTimeOffset nowUtc)
    {
        lock (this.stateLock)
        {
            this.lastStepEndedUtc = nowUtc;
            this.drawnPause = null;
        }
    }

    /// <summary>
    /// Works out how long the step which is about to start waits.
    /// </summary>
    /// <remarks>
    /// The first step which asks after a gap opened draws its pause, with both bounds included. A step
    /// which stops while it waits, e.g., because its data source was deleted, leaves the same pause
    /// to the step after it, which waits only for the rest.
    /// </remarks>
    /// <param name="nowUtc">The current time, passed in so the pause can be tested.</param>
    /// <param name="range">The bounds which apply now.</param>
    /// <param name="random">Draws the pause.</param>
    /// <returns>The pause and what is left of it, or NONE when the step starts right away.</returns>
    public PauseBeforeStep GetPauseBeforeNextStep(DateTimeOffset nowUtc, PauseRange range, Random random)
    {
        lock (this.stateLock)
        {
            if (this.lastStepEndedUtc is not { } stepEndedUtc)
                return PauseBeforeStep.NONE;

            if (this.drawnPause is null)
            {
                // Nothing is drawn, so bounds which are set before the next step still apply to this gap:
                if (range.IsNone)
                    return PauseBeforeStep.NONE;

                this.drawnPause = TimeSpan.FromSeconds(random.Next(range.MinimumSeconds, range.MaximumSeconds + 1));
            }

            var remaining = stepEndedUtc + this.drawnPause.Value - nowUtc;
            return remaining > TimeSpan.Zero ? new(this.drawnPause.Value, remaining) : PauseBeforeStep.NONE;
        }
    }
}