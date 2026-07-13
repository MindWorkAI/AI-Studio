using AIStudio.Settings;

namespace AIStudio.Tools.Services;

public sealed class TranscriptStagingCleanupService(ILogger<TranscriptStagingCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (string.IsNullOrWhiteSpace(SettingsManager.DataDirectory) && !stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        
        if (stoppingToken.IsCancellationRequested)
            return;

        var stagingRoot = Path.Combine(SettingsManager.DataDirectory!, "media-staging");
        if (!Directory.Exists(stagingRoot))
            return;

        foreach (var directory in Directory.EnumerateDirectories(stagingRoot))
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not remove orphaned media staging directory '{Directory}'.", directory);
            }
        }
    }
}