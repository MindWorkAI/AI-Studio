namespace AIStudio.Chat;

/// <summary>
/// Keeps a number up to date which nothing announces.
/// </summary>
/// <remarks>
/// A conversation is a plain list of plain objects. Nothing raises an event when a block is added,
/// when a document is attached, or when an answer grows by another sentence -- so a number derived
/// from all of that cannot be wired to the places which change it. It was tried: fifteen call sites,
/// and four review rounds each found another one which was missing.
///
/// So the number is recomputed instead of notified. Whoever thinks something may have changed nudges
/// this tracker, and the tracker decides when to do the work: many nudges in a row become one run, a
/// nudge arriving during a run becomes exactly one further run, and a minimum distance keeps a burst
/// of them from turning into a burst of counting.
///
/// The heartbeat is not distrust of the nudges. Attachments are read from disk every time they are
/// sent, so a file somebody edits in another program changes what the next message costs without
/// anything happening in AI Studio which anyone could nudge from.
/// </remarks>
/// <param name="recount">Does the actual work. Gets a token which ends it when the tracker goes away.</param>
/// <param name="quietTime">
/// How long to stay quiet after a run before honouring the next nudge. Asked again each time,
/// because what is reasonable depends on what is going on: a person who just switched a profile is
/// waiting for the number, while an answer being written moves it with every word and wants a
/// slower pace than the words arrive at.
/// </param>
/// <param name="heartbeat">How long to wait for a nudge before running anyway.</param>
public sealed class ConversationTokenTracker(Func<CancellationToken, Task> recount, Func<TimeSpan> quietTime, TimeSpan heartbeat) : IAsyncDisposable
{
    /// <summary>
    /// How long a tracker which is going away waits for its own loop.
    /// </summary>
    /// <remarks>
    /// The loop ends on cancellation, so this is only ever reached when something it called does
    /// not. Whoever is leaving the screen must not be the one who waits for that.
    /// </remarks>
    private static readonly TimeSpan SHUTDOWN_PATIENCE = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim wakeUp = new(0, 1);
    private readonly CancellationTokenSource stopping = new();

    private Task? loop;

    /// <summary>
    /// Starts the loop. Calling this twice does nothing the second time.
    /// </summary>
    public void Start() => this.loop ??= Task.Run(this.RunAsync);

    /// <summary>
    /// Says that something may have changed.
    /// </summary>
    /// <remarks>
    /// Cheap on purpose, because it is called from the render path. It says "maybe", never "yes":
    /// asking for a run which turns out to change nothing costs a few lookups, while missing one is
    /// the bug this whole class exists to make impossible.
    /// </remarks>
    public void Nudge()
    {
        //
        // One pending wake-up is all a loop can act on. A second one would only make it run again
        // with the same answer.
        //
        if (this.wakeUp.CurrentCount > 0)
            return;

        try
        {
            this.wakeUp.Release();
        }
        catch (SemaphoreFullException)
        {
            //
            // Two threads got past the check above at the same time. The one which won left the
            // wake-up we wanted, so there is nothing left to do here.
            //
        }
        catch (ObjectDisposedException)
        {
            // The tracker is going away, and a number nobody will look at needs no update.
        }
    }

    private async Task RunAsync()
    {
        var token = this.stopping.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                //
                // Sleeps until somebody nudges -- or until the heartbeat is due, which is what the
                // timeout returning false means. Both lead to the same run, so the result is not
                // even looked at.
                //
                await this.wakeUp.WaitAsync(heartbeat, token);
                if (token.IsCancellationRequested)
                    return;

                //
                // Deliberately without draining further wake-ups first. A nudge which arrives while
                // this run reads the conversation may well be about a change this run is already
                // seeing -- and then the extra run costs a few lookups. Draining would risk the
                // other case, where the change comes after the read and nobody asks again.
                //
                try
                {
                    await recount(token);
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    //
                    // One failed run must not end the loop: a tracker which died on a single bad
                    // answer would leave a stale number standing forever, which is the failure this
                    // class was built to rule out. Saying what went wrong is the job of the work
                    // itself, which is the only side that has a logger.
                    //
                }

                //
                // The quiet time is kept after the work, not before it: the first nudge of a burst
                // is answered at once, and the rest of the burst collapses into the single run which
                // follows this delay.
                //
                // It is also what paces a run which feeds itself. Showing a new number renders, and
                // a render nudges -- so while something changes continuously, this delay is the
                // whole cadence.
                //
                await Task.Delay(quietTime(), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                // The tracker was disposed underneath this loop, which is another way of stopping.
                return;
            }
        }
    }

    #region Implementation of IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await this.stopping.CancelAsync();

        if (this.loop is not null)
        {
            try
            {
                //
                // Awaited rather than abandoned, so that nothing is still counting into a component
                // which is already gone. The counting itself takes the same token, so a run which
                // sits in an IPC call ends with it -- and the patience is there for the case where
                // it does not, because a chat being closed is not worth hanging on to.
                //
                await this.loop.WaitAsync(SHUTDOWN_PATIENCE);
            }
            catch (Exception)
            {
                // The loop ends on cancellation; whatever else it carries out is of no use here.
            }
        }

        this.stopping.Dispose();
        this.wakeUp.Dispose();
    }

    #endregion
}