using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    [Inject]
    private ThreadSafeRandom Rng { get; init; } = null!;

    private static bool MinimumDelayIsManaged => ManagedConfiguration.TryGet(x => x.BatchProcessing, x => x.MinimumDelaySeconds, out var meta)
                                                 && meta.ManagedMode is not null;

    private int ManagedMinimumDelaySeconds => Math.Clamp(this.SettingsManager.ConfigurationData.BatchProcessing.MinimumDelaySeconds,
        DataBatchProcessing.MIN_DELAY_SECONDS,
        DataBatchProcessing.MAX_DELAY_SECONDS);

    private int EffectiveMinimumDelaySeconds => MinimumDelayIsManaged ? this.ManagedMinimumDelaySeconds
        : Math.Clamp(this.minimumDelaySeconds, DataBatchProcessing.MIN_DELAY_SECONDS, DataBatchProcessing.MAX_DELAY_SECONDS);

    private (int Minimum, int Maximum) GetEffectiveDelayRange()
    {
        var minimum = this.EffectiveMinimumDelaySeconds;
        var maximum = Math.Clamp(this.maximumDelaySeconds, minimum, DataBatchProcessing.MAX_DELAY_SECONDS);
        return (minimum, maximum);
    }

    /// <summary>
    /// Waits for a random, inclusive duration before the next file starts.
    /// </summary>
    private async Task WaitBeforeNextFileAsync(int minimumSeconds, int maximumSeconds, CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return;

        // ThreadSafeRandom is the application-wide singleton. Batch runs must
        // not create private Random instances because several runs may execute
        // concurrently in different assistant sessions.
        this.pauseBeforeNextFileSeconds = this.Rng.Next(minimumSeconds, maximumSeconds + 1);
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