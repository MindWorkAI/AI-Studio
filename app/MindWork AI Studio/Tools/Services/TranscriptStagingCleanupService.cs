using AIStudio.Settings;

namespace AIStudio.Tools.Services;

/// <summary>
/// One-shot startup service that removes transcript staging left by crashes or forced shutdowns.
/// </summary>
public sealed class TranscriptStagingCleanupService(ILogger<TranscriptStagingCleanupService> logger) : BackgroundService
{
    /// <summary>Waits for the data directory, performs one cleanup pass, and then exits.</summary>
    /// <param name="stoppingToken">Host shutdown token.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (string.IsNullOrWhiteSpace(SettingsManager.DataDirectory) && !stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        if (stoppingToken.IsCancellationRequested)
            return;

        var stagingRoot = Path.Combine(SettingsManager.DataDirectory!, "media-staging");
        if (!Directory.Exists(stagingRoot))
        {
            logger.LogInformation("Media transcript staging does not exist; startup cleanup has nothing to remove.");
            return;
        }

        var directories = Directory.EnumerateDirectories(stagingRoot).ToArray();
        var files = Directory.EnumerateFiles(stagingRoot).ToArray();
        logger.LogInformation("Media transcript startup cleanup found {DirectoryCount} directories and {FileCount} loose files.", directories.Length, files.Length);
        
        if (directories.Length == 0 && files.Length == 0)
        {
            logger.LogInformation("Media transcript staging is empty.");
            return;
        }

        var deletedDirectories = 0;
        var deletedFiles = 0;
        var failures = 0;
        
        foreach (var directory in directories)
        {
            try
            {
                Directory.Delete(directory, true);
                deletedDirectories++;
                logger.LogInformation("Removed orphaned media staging directory '{Directory}'.", directory);
            }
            catch (Exception exception)
            {
                failures++;
                logger.LogWarning(exception, "Could not remove orphaned media staging directory '{Directory}'.", directory);
            }
        }

        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
                deletedFiles++;
                logger.LogInformation("Removed orphaned media staging file '{File}'.", file);
            }
            catch (Exception exception)
            {
                failures++;
                logger.LogWarning(exception, "Could not remove orphaned media staging file '{File}'.", file);
            }
        }

        logger.LogInformation("Media transcript startup cleanup removed {DirectoryCount} directories and {FileCount} files with {FailureCount} failures.",
            deletedDirectories,
            deletedFiles,
            failures);
    }
}