using AIStudio.Settings.DataModel;
using AIStudio.Tools.Pauses;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    [Inject]
    private ThreadSafeRandom Rng { get; init; } = null!;

    /// <summary>
    /// The lower bound of the pause between two files as it stands.
    /// </summary>
    /// <remarks>
    /// A bound which a configuration plugin locks belongs to the organization, so it comes from the
    /// settings, whatever this assistant held before, e.g., in a session from before the lock.
    /// </remarks>
    private int MinimumDelaySeconds => DataBatchProcessing.IsMinimumPauseLocked
        ? this.SettingsManager.ConfigurationData.BatchProcessing.MinimumDelaySeconds
        : this.minimumDelaySeconds;

    /// <summary>
    /// The upper bound of the pause between two files as it stands, cf. MinimumDelaySeconds.
    /// </summary>
    private int MaximumDelaySeconds => DataBatchProcessing.IsMaximumPauseLocked
        ? this.SettingsManager.ConfigurationData.BatchProcessing.MaximumDelaySeconds
        : this.maximumDelaySeconds;

    /// <summary>
    /// The bounds of the pause between two files which apply to a run, cf. PauseRange.GetEffective.
    /// </summary>
    private PauseRange EffectivePauseRange => PauseRange.GetEffective(
        DataBatchProcessing.PAUSE_LIMITS,
        this.MinimumDelaySeconds,
        () => DataBatchProcessing.IsMinimumPauseLocked,
        this.MaximumDelaySeconds,
        () => DataBatchProcessing.IsMaximumPauseLocked);

    /// <summary>
    /// Waits for a random, inclusive duration before the next file starts.
    /// </summary>
    /// <param name="range">The bounds of the pause, as they apply to the run.</param>
    /// <param name="token">Ends the wait when the user stops the run.</param>
    private async Task WaitBeforeNextFileAsync(PauseRange range, CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return;

        // ThreadSafeRandom is the application-wide singleton. Batch runs must
        // not create private Random instances because several runs may execute
        // concurrently in different assistant sessions.
        this.pauseBeforeNextFileSeconds = this.Rng.Next(range.MinimumSeconds, range.MaximumSeconds + 1);
        this.Logger.LogInformation("Batch processing waits {DelaySeconds} seconds before starting the next file.", this.pauseBeforeNextFileSeconds);

        await this.CheckpointAssistantSession();
        await this.RefreshAssistantUIAsync();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(this.pauseBeforeNextFileSeconds), token);
        }
        finally
        {
            this.pauseBeforeNextFileSeconds = 0;
            await this.CheckpointAssistantSession();
            await this.RefreshAssistantUIAsync();
        }
    }
}