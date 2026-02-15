using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed class EnterpriseEnvironmentService(ILogger<EnterpriseEnvironmentService> logger, RustService rustService) : BackgroundService
{
    public static List<EnterpriseEnvironment> CURRENT_ENVIRONMENTS = [];

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

            //
            // Step 1: Handle deletions first.
            //
            List<Guid> deleteConfigIds;
            try
            {
                deleteConfigIds = await rustService.EnterpriseEnvDeleteConfigIds();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to fetch the enterprise delete configuration IDs from the Rust service.");
                await MessageBus.INSTANCE.SendMessage(null, Event.RUST_SERVICE_UNAVAILABLE, "EnterpriseEnvDeleteConfigIds failed");
                return;
            }

            foreach (var deleteId in deleteConfigIds)
            {
                var isPluginInUse = PluginFactory.AvailablePlugins.Any(plugin => plugin.Id == deleteId);
                if (isPluginInUse)
                {
                    logger.LogWarning("The enterprise environment configuration ID '{DeleteConfigId}' must be removed.", deleteId);
                    PluginFactory.RemovePluginAsync(deleteId);
                }
            }

            //
            // Step 2: Fetch all active configurations.
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
            // Step 3: Determine ETags and build the next environment list.
            //
            var nextEnvironments = new List<EnterpriseEnvironment>();
            foreach (var config in fetchedConfigs)
            {
                if (!config.IsActive)
                {
                    logger.LogWarning("Skipping inactive enterprise configuration with ID '{ConfigId}'. There is either no valid server URL or config ID set.", config.ConfigurationId);
                    continue;
                }

                var etag = await PluginFactory.DetermineConfigPluginETagAsync(config.ConfigurationId, config.ConfigurationServerUrl);
                nextEnvironments.Add(config with { ETag = etag });
            }

            if (nextEnvironments.Count == 0)
            {
                if (CURRENT_ENVIRONMENTS.Count > 0)
                {
                    logger.LogWarning("AI Studio no longer has any enterprise configurations. Removing previously active configs.");

                    // Remove plugins for configs that were previously active:
                    foreach (var oldEnv in CURRENT_ENVIRONMENTS)
                    {
                        var isPluginInUse = PluginFactory.AvailablePlugins.Any(plugin => plugin.Id == oldEnv.ConfigurationId);
                        if (isPluginInUse)
                            PluginFactory.RemovePluginAsync(oldEnv.ConfigurationId);
                    }
                }
                else
                    logger.LogInformation("AI Studio runs without any enterprise configurations.");

                CURRENT_ENVIRONMENTS = [];
                return;
            }

            //
            // Step 4: Compare with current environments and process changes.
            //
            var currentIds = CURRENT_ENVIRONMENTS.Select(e => e.ConfigurationId).ToHashSet();
            var nextIds = nextEnvironments.Select(e => e.ConfigurationId).ToHashSet();

            // Remove plugins for configs that are no longer present:
            foreach (var oldEnv in CURRENT_ENVIRONMENTS)
            {
                if (!nextIds.Contains(oldEnv.ConfigurationId))
                {
                    logger.LogInformation("Enterprise configuration '{ConfigId}' was removed.", oldEnv.ConfigurationId);
                    var isPluginInUse = PluginFactory.AvailablePlugins.Any(plugin => plugin.Id == oldEnv.ConfigurationId);
                    if (isPluginInUse)
                        PluginFactory.RemovePluginAsync(oldEnv.ConfigurationId);
                }
            }

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

                if (isFirstRun)
                    MessageBus.INSTANCE.DeferMessage(null, Event.STARTUP_ENTERPRISE_ENVIRONMENT, nextEnv);
                else
                    await PluginFactory.TryDownloadingConfigPluginAsync(nextEnv.ConfigurationId, nextEnv.ConfigurationServerUrl);
            }

            CURRENT_ENVIRONMENTS = nextEnvironments;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while updating the enterprise environment.");
        }
    }
}