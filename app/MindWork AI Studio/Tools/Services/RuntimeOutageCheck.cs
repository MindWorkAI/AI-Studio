namespace AIStudio.Tools.Services;

/// <summary>
/// Decides whether the runtime is gone after a call to it failed.
/// </summary>
/// <remarks>
/// A failed call alone proves little. All calls share one HTTP/2 connection, which a flood of requests can
/// congest and which the runtime may close, while the runtime itself keeps running. Therefore, a report
/// starts a round of probes, which ask the runtime over a connection of their own. The first answer ends the
/// round, and the next report starts afresh: failed calls never add up over the lifetime of the server. Only
/// when no probe of a round gets an answer does the runtime count as gone, and the server shuts down.
/// </remarks>
/// <param name="isRuntimeAlive">Asks the runtime whether it is alive; true when it answered.</param>
/// <param name="shutDown">Shuts the server down once the runtime counts as gone.</param>
/// <param name="schedule">How many probes a round makes and how long it waits between them.</param>
/// <param name="stopToken">Ends a running round when the server stops.</param>
/// <param name="logger">The logger.</param>
public sealed class RuntimeOutageCheck(Func<CancellationToken, Task<bool>> isRuntimeAlive, Action shutDown, RuntimeProbeSchedule schedule, CancellationToken stopToken, ILogger logger)
{
    private readonly Lock roundLock = new();

    private Task runningRound = Task.CompletedTask;
    private int reportsInRound;
    private int shutDownStarted;

    /// <summary>
    /// Reports that a call to the runtime failed, and starts a round of probes unless one is running.
    /// </summary>
    /// <param name="reason">Why the call failed, for the log.</param>
    /// <returns>The round this report joined. It ends once the runtime answered or counts as gone, and it never fails.</returns>
    public Task Report(string reason)
    {
        if (Volatile.Read(ref this.shutDownStarted) != 0)
            return Task.CompletedTask;

        lock (this.roundLock)
        {
            if (!this.runningRound.IsCompleted)
            {
                this.reportsInRound++;
                logger.LogDebug("Another call to the runtime failed while a round of probes is running. Reason = '{Reason}'.", reason);
                return this.runningRound;
            }

            this.reportsInRound = 1;
            this.runningRound = Task.Run(() => this.RunRoundAsync(reason));
            this.runningRound.Observe($"{nameof(RuntimeOutageCheck)}: checking whether the runtime is alive");
            return this.runningRound;
        }
    }

    private async Task RunRoundAsync(string reason)
    {
        logger.LogWarning("A call to the runtime failed. Reason = '{Reason}'. Checking over a connection of its own whether the runtime is still alive.", reason);
        try
        {
            for (var probe = 1; probe <= schedule.Probes; probe++)
            {
                if (probe > 1)
                    await Task.Delay(schedule.Interval, stopToken);

                if (await this.ProbeAsync())
                {
                    logger.LogInformation("The runtime answered probe {Probe} of {Probes} and is still alive. The {Reports} failed call(s) of this round were transient.", probe, schedule.Probes, this.ReportsInRound);
                    return;
                }

                logger.LogWarning("The runtime did not answer probe {Probe} of {Probes}.", probe, schedule.Probes);
            }
        }
        catch (Exception) when (stopToken.IsCancellationRequested)
        {
            // The server stops anyway, so the outcome of this round no longer matters.
            return;
        }

        if (Interlocked.Exchange(ref this.shutDownStarted, 1) != 0)
            return;

        logger.LogError("The runtime did not answer any of {Probes} probes, {Interval} apart, and counts as gone. Failed calls in this round: {Reports}; the first one failed with reason '{Reason}'. Shutting down the server.", schedule.Probes, schedule.Interval, this.ReportsInRound, reason);
        shutDown();
    }

    /// <summary>
    /// Asks the runtime once whether it is alive. A probe which fails in any way counts as no answer.
    /// </summary>
    private async Task<bool> ProbeAsync()
    {
        try
        {
            return await isRuntimeAlive(stopToken);
        }
        catch (Exception e) when (!stopToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "The probe whether the runtime is alive failed.");
            return false;
        }
    }

    private int ReportsInRound
    {
        get
        {
            lock (this.roundLock)
                return this.reportsInRound;
        }
    }
}