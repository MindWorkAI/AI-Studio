using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed class EnterpriseEnvironmentService(ILogger<EnterpriseEnvironmentService> logger, RustService rustService) : BackgroundService
{
    public static List<EnterpriseEnvironment> CURRENT_ENVIRONMENTS = [];
    
    public static bool HasValidEnterpriseSnapshot { get; private set; }

#if DEBUG
    private static readonly TimeSpan CHECK_INTERVAL = TimeSpan.FromMinutes(6);
#else
    private static readonly TimeSpan CHECK_INTERVAL = TimeSpan.FromMinutes(16);
#endif
    
    #region Overrides of BackgroundService

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("The enterprise environment service was initialized.");
        
        await this.StartUpdating(isFirstRun: true);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CHECK_INTERVAL, stoppingToken);
            await this.StartUpdating();
        }
    }

    #endregion

    private async Task StartUpdating(bool isFirstRun = false)
    {
        try
        {
            logger.LogInformation("Start updating of the enterprise environment.");
            HasValidEnterpriseSnapshot = false;

            //
            // Step 1: Fetch all active configurations.
            //
            List<EnterpriseEnvironment> fetchedConfigs;
            try
            {
                fetchedConfigs = await rustService.EnterpriseEnvConfigs();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to fetch the enterprise configurations from the Rust service.");
                await MessageBus.INSTANCE.SendMessage(null, Event.RUST_SERVICE_UNAVAILABLE, "EnterpriseEnvConfigs failed");
                return;
            }

            //
            // Step 2: Determine ETags and build the next environment list.
            // IMPORTANT: if we cannot read the ETag for any active configuration,
            // do not mutate the plugin state and keep everything as-is.
            //
            var nextEnvironments = new List<EnterpriseEnvironment>();
            foreach (var config in fetchedConfigs)
            {
                if (!config.IsActive)
                {
                    logger.LogWarning("Skipping inactive enterprise configuration with ID '{ConfigId}'. There is either no valid server URL or config ID set.", config.ConfigurationId);
                    continue;
                }

                var etagResponse = await PluginFactory.DetermineConfigPluginETagAsync(config.ConfigurationId, config.ConfigurationServerUrl);
                if (!etagResponse.Success)
                {
                    logger.LogWarning("Failed to read enterprise config metadata for '{ConfigId}'. Keeping current plugins unchanged.", config.ConfigurationId);
                    return;
                }

                nextEnvironments.Add(config with { ETag = etagResponse.ETag });
            }

            //
            // Step 3: Compare with current environments and process changes.
            // Download first. We only clean up obsolete plugins after all required
            // downloads have been completed successfully.
            //
            var currentIds = CURRENT_ENVIRONMENTS.Select(e => e.ConfigurationId).ToHashSet();
            var nextIds = nextEnvironments.Select(e => e.ConfigurationId).ToHashSet();
            var shouldDeferStartupDownloads = isFirstRun && !PluginFactory.IsInitialized;

            // Process new or changed configs:
            foreach (var nextEnv in nextEnvironments)
            {
                var currentEnv = CURRENT_ENVIRONMENTS.FirstOrDefault(e => e.ConfigurationId == nextEnv.ConfigurationId);
                if (currentEnv == nextEnv) // Hint: This relies on the record equality to check if anything relevant has changed (e.g. server URL or ETag).
                {
                    logger.LogInformation("Enterprise configuration '{ConfigId}' has not changed. No update required.", nextEnv.ConfigurationId);
                    continue;
                }

                var isNew = !currentIds.Contains(nextEnv.ConfigurationId);
                if(isNew)
                    logger.LogInformation("Detected new enterprise configuration with ID '{ConfigId}' and server URL '{ServerUrl}'.", nextEnv.ConfigurationId, nextEnv.ConfigurationServerUrl);
                else
                    logger.LogInformation("Detected change in enterprise configuration with ID '{ConfigId}'. Server URL or ETag has changed.", nextEnv.ConfigurationId);

                if (shouldDeferStartupDownloads)
                    MessageBus.INSTANCE.DeferMessage(null, Event.STARTUP_ENTERPRISE_ENVIRONMENT, nextEnv);
                else
                {
                    var wasDownloadSuccessful = await PluginFactory.TryDownloadingConfigPluginAsync(nextEnv.ConfigurationId, nextEnv.ConfigurationServerUrl);
                    if (!wasDownloadSuccessful)
                    {
                        logger.LogWarning("Failed to update enterprise configuration '{ConfigId}'. Keeping current plugins unchanged.", nextEnv.ConfigurationId);
                        return;
                    }
                }
            }

            // Cleanup is only allowed after a successful sync cycle:
            if (PluginFactory.IsInitialized && !shouldDeferStartupDownloads)
                PluginFactory.RemoveUnreferencedManagedConfigurationPlugins(nextIds);

            if (nextEnvironments.Count == 0)
                logger.LogInformation("AI Studio runs without any enterprise configurations.");

            CURRENT_ENVIRONMENTS = nextEnvironments;
            HasValidEnterpriseSnapshot = true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while updating the enterprise environment.");
        }
    }
}