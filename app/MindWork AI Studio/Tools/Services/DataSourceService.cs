using AIStudio.Assistants.ERI;
using AIStudio.Provider;
using AIStudio.Provider.SelfHosted;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.Services;

public sealed class DataSourceService
{
    private readonly RustService rustService;
    private readonly SettingsManager settingsManager;
    private readonly ILogger<DataSourceService> logger;

    public DataSourceService(SettingsManager settingsManager, ILogger<DataSourceService> logger, RustService rustService)
    {
        this.logger = logger;
        this.rustService = rustService;
        this.settingsManager = settingsManager;
        
        this.logger.LogInformation("The data source service has been initialized.");
    }
    
    /// <summary>
    /// Returns a list of data sources that are allowed for the selected LLM provider.
    /// It also returns the data sources selected before when they are still allowed.
    /// </summary>
    /// <param name="selectedLLMProvider">The selected LLM provider.</param>
    /// <param name="previousSelectedDataSources">The data sources selected before.</param>
    /// <returns>The allowed data sources and the data sources selected before -- when they are still allowed.</returns>
    public async Task<AllowedSelectedDataSources> GetDataSources(AIStudio.Settings.Provider selectedLLMProvider, IReadOnlyCollection<IDataSource>? previousSelectedDataSources = null)
    {
        //
        // Case: Somehow the selected LLM provider was not set. The default provider
        //       does not mean anything. We cannot filter the data sources by any means.
        //       We return an empty list. Better safe than sorry.
        //
        if (selectedLLMProvider == default)
        {
            this.logger.LogWarning("The selected LLM provider is not set. We cannot filter the data sources by any means.");
            return new([], []);
        }
        
        return await this.GetDataSources(selectedLLMProvider.IsSelfHosted, previousSelectedDataSources);
    }
    
    /// <summary>
    /// Returns a list of data sources that are allowed for the selected LLM provider.
    /// It also returns the data sources selected before when they are still allowed.
    /// </summary>
    /// <param name="selectedLLMProvider">The selected LLM provider.</param>
    /// <param name="previousSelectedDataSources">The data sources selected before.</param>
    /// <returns>The allowed data sources and the data sources selected before -- when they are still allowed.</returns>
    public async Task<AllowedSelectedDataSources> GetDataSources(IProvider selectedLLMProvider, IReadOnlyCollection<IDataSource>? previousSelectedDataSources = null)
    {
        //
        // Case: Somehow the selected LLM provider was not set. The default provider
        //       does not mean anything. We cannot filter the data sources by any means.
        //       We return an empty list. Better safe than sorry.
        //
        if (selectedLLMProvider is NoProvider)
        {
            this.logger.LogWarning("The selected LLM provider is the default provider. We cannot filter the data sources by any means.");
            return new([], []);
        }
        
        return await this.GetDataSources(selectedLLMProvider is ProviderSelfHosted, previousSelectedDataSources);
    }
    
    private async Task<AllowedSelectedDataSources> GetDataSources(bool usingSelfHostedProvider, IReadOnlyCollection<IDataSource>? previousSelectedDataSources = null)
    {
        var allDataSources = this.settingsManager.ConfigurationData.DataSources;
        var filteredDataSources = new List<IDataSource>(allDataSources.Count);
        var filteredSelectedDataSources = new List<IDataSource>(previousSelectedDataSources?.Count ?? 0);
        var tasks = new List<Task<IDataSource?>>(allDataSources.Count);
        
        // Start all checks in parallel:
        foreach (var source in allDataSources)
            tasks.Add(this.CheckOneDataSource(source, usingSelfHostedProvider));
        
        // Wait for all checks and collect the results:
        foreach (var task in tasks)
        {
            var source = await task;
            if (source is not null)
            {
                filteredDataSources.Add(source);
                if (previousSelectedDataSources is not null && previousSelectedDataSources.Contains(source))
                    filteredSelectedDataSources.Add(source);
            }
        }
        
        return new(filteredDataSources, filteredSelectedDataSources);
    }
    
    private async Task<IDataSource?> CheckOneDataSource(IDataSource source, bool usingSelfHostedProvider)
    {
        //
        // Unfortunately, we have to live-check any ERI source for its security requirements.
        // Because the ERI server operator might change the security requirements at any time.
        //
        SecurityRequirements? eriSourceRequirements = null;
        if (source is DataSourceERI_V1 eriSource)
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var client = ERIClientFactory.Get(ERIVersion.V1, eriSource);
            if(client is null)
            {
                this.logger.LogError($"Could not create ERI client for source '{source.Name}' (id={source.Id}). We skip this source.");
                return null;
            }

            this.logger.LogInformation($"Authenticating with ERI source '{source.Name}' (id={source.Id})...");
            var loginResult = await client.AuthenticateAsync(this.rustService, cancellationToken: cancellationTokenSource.Token);
            if (!loginResult.Successful)
            {
                this.logger.LogWarning($"Authentication with ERI source '{source.Name}' (id={source.Id}) failed. We skip this source. Reason: {loginResult.Message}");
                return null;
            }

            this.logger.LogInformation($"Checking security requirements for ERI source '{source.Name}' (id={source.Id})...");
            var securityRequest = await client.GetSecurityRequirementsAsync(cancellationTokenSource.Token);
            if (!securityRequest.Successful)
            {
                this.logger.LogWarning($"Could not retrieve security requirements for ERI source '{source.Name}' (id={source.Id}). We skip this source. Reason: {loginResult.Message}");
                return null;
            }

            eriSourceRequirements = securityRequest.Data;
            this.logger.LogInformation($"Security requirements for ERI source '{source.Name}' (id={source.Id}) retrieved successfully.");
        }
        
        switch (source.SecurityPolicy)
        {
            case DataSourceSecurity.ALLOW_ANY:
                
                //
                // Case: The data source allows any provider type. We want to use a self-hosted provider.
                //       There is no issue with this source. Accept it.
                //
                if(usingSelfHostedProvider)
                    return source;

                //
                // Case: This is a local data source. When the source allows any provider type, we can use it.
                //       Accept it.
                //
                if(eriSourceRequirements is null)
                    return source;

                //
                // Case: The ERI source requires a self-hosted provider. This misconfiguration happens
                //       when the ERI server operator changes the security requirements. The ERI server
                //       operator owns the data -- we have to respect their rules. We skip this source.
                //
                if (eriSourceRequirements is { AllowedProviderType: ProviderType.SELF_HOSTED })
                {
                    this.logger.LogWarning($"The ERI source '{source.Name}' (id={source.Id}) requires a self-hosted provider. We skip this source.");
                    return null;
                }    
                
                //
                // Case: The ERI source allows any provider type. The data source configuration is correct.
                //       Accept it.
                //
                if(eriSourceRequirements is { AllowedProviderType: ProviderType.ANY })
                    return source;

                //
                // Case: Missing rules. We skip this source. Better safe than sorry.
                //
                this.logger.LogDebug($"The ERI source '{source.Name}' (id={source.Id}) was filtered out due to missing rules.");
                return null;

            //
            // Case: The data source requires a self-hosted provider. We want to use a self-hosted provider.
            //       There is no issue with this source. Accept it.
            //
            case DataSourceSecurity.SELF_HOSTED when usingSelfHostedProvider:
                return source;
            
            //
            // Case: The data source requires a self-hosted provider. We want to use a cloud provider.
            //       We skip this source.
            //
            case DataSourceSecurity.SELF_HOSTED when !usingSelfHostedProvider:
                this.logger.LogWarning($"The data source '{source.Name}' (id={source.Id}) requires a self-hosted provider. We skip this source.");
                return null;
            
            //
            // Case: The data source did not specify a security policy. We skip this source.
            //       Better safe than sorry.
            //
            case DataSourceSecurity.NOT_SPECIFIED:
                this.logger.LogWarning($"The data source '{source.Name}' (id={source.Id}) has no security policy. We skip this source.");
                return null;
            
            //
            // Case: Some developer forgot to implement a security policy. We skip this source.
            //       Better safe than sorry.
            //
            default:
                this.logger.LogWarning($"The data source '{source.Name}' (id={source.Id}) was filtered out due unknown security policy.");
                return null;
        }
    }
}