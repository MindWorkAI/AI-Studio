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
            // Step 2: Determine ETags and build the list of reachable configurations.
            // IMPORTANT: when one config server fails, we continue with the others.
            //
            var reachableEnvironments = new List<EnterpriseEnvironment>();
            var failedConfigIds = new HashSet<Guid>();
            var currentEnvironmentsById = CURRENT_ENVIRONMENTS
                .GroupBy(env => env.ConfigurationId)
                .ToDictionary(group => group.Key, group => group.Last());
            
            var activeFetchedEnvironmentsById = fetchedConfigs
                .Where(config => config.IsActive)
                .GroupBy(config => config.ConfigurationId)
                .ToDictionary(group => group.Key, group => group.Last());
            
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
                    failedConfigIds.Add(config.ConfigurationId);
                    logger.LogWarning("Failed to read enterprise config metadata for '{ConfigId}' from '{ServerUrl}': {Issue}. Keeping the current plugin state for this configuration.", config.ConfigurationId, config.ConfigurationServerUrl, etagResponse.Issue ?? "Unknown issue");
                    continue;
                }

                reachableEnvironments.Add(config with { ETag = etagResponse.ETag });
            }

            //
            // Step 3: Compare with current environments and process changes.
            // Download per configuration. A single failure must not block others.
            //
            var shouldDeferStartupDownloads = isFirstRun && !PluginFactory.IsInitialized;
            var effectiveEnvironmentsById = new Dictionary<Guid, EnterpriseEnvironment>();

            // Process new or changed configs:
            foreach (var nextEnv in reachableEnvironments)
            {
                var hasCurrentEnvironment = currentEnvironmentsById.TryGetValue(nextEnv.ConfigurationId, out var currentEnv);
                if (hasCurrentEnvironment && currentEnv == nextEnv) // Hint: This relies on the record equality to check if anything relevant has changed (e.g. server URL or ETag).
                {
                    logger.LogInformation("Enterprise configuration '{ConfigId}' has not changed. No update required.", nextEnv.ConfigurationId);
                    effectiveEnvironmentsById[nextEnv.ConfigurationId] = nextEnv;
                    continue;
                }

                if(!hasCurrentEnvironment)
                    logger.LogInformation("Detected new enterprise configuration with ID '{ConfigId}' and server URL '{ServerUrl}'.", nextEnv.ConfigurationId, nextEnv.ConfigurationServerUrl);
                else
                    logger.LogInformation("Detected change in enterprise configuration with ID '{ConfigId}'. Server URL or ETag has changed.", nextEnv.ConfigurationId);

                if (shouldDeferStartupDownloads)
                {
                    MessageBus.INSTANCE.DeferMessage(null, Event.STARTUP_ENTERPRISE_ENVIRONMENT, nextEnv);
                    effectiveEnvironmentsById[nextEnv.ConfigurationId] = nextEnv;
                }
                else
                {
                    var wasDownloadSuccessful = await PluginFactory.TryDownloadingConfigPluginAsync(nextEnv.ConfigurationId, nextEnv.ConfigurationServerUrl);
                    if (!wasDownloadSuccessful)
                    {
                        failedConfigIds.Add(nextEnv.ConfigurationId);
                        if (hasCurrentEnvironment)
                        {
                            logger.LogWarning("Failed to update enterprise configuration '{ConfigId}'. Keeping the previously active version.", nextEnv.ConfigurationId);
                            effectiveEnvironmentsById[nextEnv.ConfigurationId] = currentEnv;
                        }
                        else
                            logger.LogWarning("Failed to download the new enterprise configuration '{ConfigId}'. Skipping activation for now.", nextEnv.ConfigurationId);

                        continue;
                    }

                    effectiveEnvironmentsById[nextEnv.ConfigurationId] = nextEnv;
                }
            }

            // Retain configurations for all failed IDs. On cold start there might be no
            // previous in-memory snapshot yet, so we also keep the current fetched entry
            // to protect it from cleanup while the server is unreachable.
            foreach (var failedConfigId in failedConfigIds)
            {
                if (effectiveEnvironmentsById.ContainsKey(failedConfigId))
                    continue;

                if (!currentEnvironmentsById.TryGetValue(failedConfigId, out var retainedEnvironment))
                {
                    if (!activeFetchedEnvironmentsById.TryGetValue(failedConfigId, out retainedEnvironment))
                        continue;

                    logger.LogWarning("Could not refresh enterprise configuration '{ConfigId}'. Protecting it from cleanup until connectivity is restored.", failedConfigId);
                }
                else
                    logger.LogWarning("Could not refresh enterprise configuration '{ConfigId}'. Keeping the previously active version.", failedConfigId);

                effectiveEnvironmentsById[failedConfigId] = retainedEnvironment;
            }

            var effectiveEnvironments = effectiveEnvironmentsById.Values.ToList();

            // Cleanup is only allowed after a successful sync cycle:
            if (PluginFactory.IsInitialized && !shouldDeferStartupDownloads)
                PluginFactory.RemoveUnreferencedManagedConfigurationPlugins(effectiveEnvironmentsById.Keys.ToHashSet());

            if (effectiveEnvironments.Count == 0)
                logger.LogInformation("AI Studio runs without any enterprise configurations.");

            CURRENT_ENVIRONMENTS = effectiveEnvironments;
            HasValidEnterpriseSnapshot = true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while updating the enterprise environment.");
        }
    }
}