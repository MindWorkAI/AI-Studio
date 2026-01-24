using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed class EnterpriseEnvironmentService(ILogger<EnterpriseEnvironmentService> logger, RustService rustService) : BackgroundService
{
    public static EnterpriseEnvironment CURRENT_ENVIRONMENT;

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
        
            Guid enterpriseRemoveConfigId;
            try
            {
                enterpriseRemoveConfigId = await rustService.EnterpriseEnvRemoveConfigId();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to fetch the enterprise remove configuration ID from the Rust service.");
                return;
            }
            
            var isPlugin2RemoveInUse = PluginFactory.AvailablePlugins.Any(plugin => plugin.Id == enterpriseRemoveConfigId);
            if (enterpriseRemoveConfigId != Guid.Empty && isPlugin2RemoveInUse)
            {
                logger.LogWarning("The enterprise environment configuration ID '{EnterpriseRemoveConfigId}' must be removed.", enterpriseRemoveConfigId);
                PluginFactory.RemovePluginAsync(enterpriseRemoveConfigId);
            }

            string? enterpriseConfigServerUrl;
            try
            {
                enterpriseConfigServerUrl = await rustService.EnterpriseEnvConfigServerUrl();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to fetch the enterprise configuration server URL from the Rust service.");
                return;
            }

            Guid enterpriseConfigId;
            try
            {
                enterpriseConfigId = await rustService.EnterpriseEnvConfigId();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to fetch the enterprise configuration ID from the Rust service.");
                return;
            }
            
            var etag = await PluginFactory.DetermineConfigPluginETagAsync(enterpriseConfigId, enterpriseConfigServerUrl);
            var nextEnterpriseEnvironment = new EnterpriseEnvironment(enterpriseConfigServerUrl, enterpriseConfigId, etag);
            if (CURRENT_ENVIRONMENT != nextEnterpriseEnvironment)
            {
                logger.LogInformation("The enterprise environment has changed. Updating the current environment.");
                CURRENT_ENVIRONMENT = nextEnterpriseEnvironment;
            
                switch (enterpriseConfigServerUrl)
                {
                    case null when enterpriseConfigId == Guid.Empty:
                    case not null when string.IsNullOrWhiteSpace(enterpriseConfigServerUrl) && enterpriseConfigId == Guid.Empty:
                        logger.LogInformation("AI Studio runs without an enterprise configuration.");
                        break;

                    case null:
                        logger.LogWarning("AI Studio runs with an enterprise configuration id ('{EnterpriseConfigId}'), but the configuration server URL is not set.", enterpriseConfigId);
                        break;

                    case not null when !string.IsNullOrWhiteSpace(enterpriseConfigServerUrl) && enterpriseConfigId == Guid.Empty:
                        logger.LogWarning("AI Studio runs with an enterprise configuration server URL ('{EnterpriseConfigServerUrl}'), but the configuration ID is not set.", enterpriseConfigServerUrl);
                        break;

                    default:
                        logger.LogInformation("AI Studio runs with an enterprise configuration id ('{EnterpriseConfigId}') and configuration server URL ('{EnterpriseConfigServerUrl}').", enterpriseConfigId, enterpriseConfigServerUrl);
                        
                        if(isFirstRun)
                            MessageBus.INSTANCE.DeferMessage(null, Event.STARTUP_ENTERPRISE_ENVIRONMENT, new EnterpriseEnvironment(enterpriseConfigServerUrl, enterpriseConfigId, etag));
                        else
                            await PluginFactory.TryDownloadingConfigPluginAsync(enterpriseConfigId, enterpriseConfigServerUrl);
                        break;
                }
            }
            else
                logger.LogInformation("The enterprise environment has not changed. No update required.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while updating the enterprise environment.");
        }
    }
}