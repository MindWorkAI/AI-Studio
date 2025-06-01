using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed class EnterpriseEnvironmentService(ILogger<TemporaryChatService> logger, RustService rustService) : BackgroundService
{
    public static EnterpriseEnvironment CURRENT_ENVIRONMENT;
    
    private static readonly TimeSpan CHECK_INTERVAL = TimeSpan.FromMinutes(16);
    
    #region Overrides of BackgroundService

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("The enterprise environment service was initialized.");
        
        await this.StartUpdating();
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CHECK_INTERVAL, stoppingToken);
            await this.StartUpdating();
        }
    }

    #endregion

    private async Task StartUpdating()
    {
        try
        {
            logger.LogInformation("Starting update of the enterprise environment.");
        
            var enterpriseRemoveConfigId = await rustService.EnterpriseEnvRemoveConfigId();
            var isPlugin2RemoveInUse = PluginFactory.AvailablePlugins.Any(plugin => plugin.Id == enterpriseRemoveConfigId);
            if (enterpriseRemoveConfigId != Guid.Empty && isPlugin2RemoveInUse)
            {
                logger.LogWarning($"The enterprise environment configuration ID '{enterpriseRemoveConfigId}' must be removed.");
                PluginFactory.RemovePluginAsync(enterpriseRemoveConfigId);
            }
        
            var enterpriseConfigServerUrl = await rustService.EnterpriseEnvConfigServerUrl();
            var enterpriseConfigId = await rustService.EnterpriseEnvConfigId();
            var nextEnterpriseEnvironment = new EnterpriseEnvironment(enterpriseConfigServerUrl, enterpriseConfigId);
            if (CURRENT_ENVIRONMENT != nextEnterpriseEnvironment)
            {
                logger.LogInformation("The enterprise environment has changed. Updating the current environment.");
                CURRENT_ENVIRONMENT = nextEnterpriseEnvironment;
            
                switch (enterpriseConfigServerUrl)
                {
                    case null when enterpriseConfigId == Guid.Empty:
                        logger.LogInformation("AI Studio runs without an enterprise configuration.");
                        break;

                    case null:
                        logger.LogWarning($"AI Studio runs with an enterprise configuration id ('{enterpriseConfigId}'), but the configuration server URL is not set.");
                        break;

                    case not null when enterpriseConfigId == Guid.Empty:
                        logger.LogWarning($"AI Studio runs with an enterprise configuration server URL ('{enterpriseConfigServerUrl}'), but the configuration ID is not set.");
                        break;

                    default:
                        logger.LogInformation($"AI Studio runs with an enterprise configuration id ('{enterpriseConfigId}') and configuration server URL ('{enterpriseConfigServerUrl}').");
                        await PluginFactory.TryDownloadingConfigPluginAsync(enterpriseConfigId, enterpriseConfigServerUrl);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while updating the enterprise environment.");
        }
    }
}