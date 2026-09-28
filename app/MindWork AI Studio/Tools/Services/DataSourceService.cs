using AIStudio.Assistants.ERI;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.Services;

public sealed class DataSourceService
{
    //
    // Trust is recorded for every participating provider, while only the chat provider's trust
    // decides about external data sources below. The agent which validates retrieval contexts
    // checks its own provider before it runs, so nothing slips through today. Keeping the value
    // named here is what makes that asymmetry visible.
    //
    // ReSharper disable once NotAccessedPositionalProperty.Local
    private readonly record struct ParticipatingProvider(string Role, bool IsTrusted, ConfidenceLevel ConfidenceLevel);

    private readonly DataSourceEmbeddingService embeddingService;
    private readonly RustService rustService;
    private readonly SettingsManager settingsManager;
    private readonly ILogger<DataSourceService> logger;

    public DataSourceService(SettingsManager settingsManager, ILogger<DataSourceService> logger, RustService rustService, DataSourceEmbeddingService embeddingService)
    {
        this.logger = logger;
        this.rustService = rustService;
        this.settingsManager = settingsManager;
        this.embeddingService = embeddingService;

        this.logger.LogInformation("The data source service has been initialized.");
    }
    
    /// <summary>
    /// Returns a list of data sources that are allowed for the selected LLM provider.
    /// It also returns the data sources selected before when they are still allowed.
    /// </summary>
    /// <param name="selectedLLMProvider">The selected LLM provider.</param>
    /// <param name="dataSourceOptions">The active data source options, which determine which agent providers participate.</param>
    /// <param name="retrievalMode">How the data sources are searched in effect, which decides whether any agent participates at all.</param>
    /// <param name="previousSelectedDataSources">The data sources selected before.</param>
    /// <returns>The allowed data sources and the data sources selected before -- when they are still allowed.</returns>
    public async Task<AllowedSelectedDataSources> GetDataSources(AIStudio.Settings.Provider selectedLLMProvider, DataSourceOptions dataSourceOptions, DataSourceRetrievalMode retrievalMode, IReadOnlyCollection<IDataSource>? previousSelectedDataSources = null)
    {
        //
        // Case: Somehow the selected LLM provider was not set. The default provider
        //       does not mean anything. We cannot filter the data sources by any means.
        //       We return an empty list. Better safe than sorry.
        //
        if (selectedLLMProvider == Settings.Provider.NONE)
        {
            this.logger.LogWarning("The selected LLM provider is not set. We cannot filter the data sources by any means.");
            return new([], [], [], []);
        }
        
        var usingTrustedProvider = selectedLLMProvider.IsTrustedForDataSourceSecurityChecks(this.settingsManager);
        var participatingProviders = this.GetParticipatingProviders(selectedLLMProvider.Id, dataSourceOptions, retrievalMode,
            new("chat provider", usingTrustedProvider, selectedLLMProvider.GetConfidenceLevel(this.settingsManager)));
        return await this.GetDataSources(usingTrustedProvider, participatingProviders, previousSelectedDataSources);
    }

    /// <summary>
    /// Returns the requested data sources that are allowed for the selected LLM provider.
    /// Unlike see GetDataSources(AIStudio.Settings.Provider, IReadOnlyCollection{IDataSource}),
    /// this method checks only the supplied data sources.
    /// </summary>
    /// <param name="selectedLLMProvider">The selected LLM provider.</param>
    /// <param name="dataSourceOptions">The active data source options, which determine which agent providers participate.</param>
    /// <param name="retrievalMode">How the data sources are searched in effect, which decides whether any agent participates at all.</param>
    /// <param name="requestedDataSources">The data sources to check.</param>
    /// <returns>The requested data sources that are allowed for the provider.</returns>
    public async Task<IReadOnlyList<IDataSource>> GetAllowedDataSources(AIStudio.Settings.Provider selectedLLMProvider, DataSourceOptions dataSourceOptions, DataSourceRetrievalMode retrievalMode, IReadOnlyCollection<IDataSource> requestedDataSources)
    {
        if (selectedLLMProvider == Settings.Provider.NONE)
        {
            this.logger.LogWarning("The selected LLM provider is not set. We cannot filter the data sources by any means.");
            return [];
        }

        var usingTrustedProvider = selectedLLMProvider.IsTrustedForDataSourceSecurityChecks(this.settingsManager);
        var participatingProviders = this.GetParticipatingProviders(selectedLLMProvider.Id, dataSourceOptions, retrievalMode,
            new("chat provider", usingTrustedProvider, selectedLLMProvider.GetConfidenceLevel(this.settingsManager)));
        var allowedDataSources = await this.GetAllowedDataSources(usingTrustedProvider, participatingProviders, requestedDataSources);

        //
        // Whoever asks this way has no list to show, so a data source which cannot be searched is
        // dropped rather than marked. Handing it back would start a chat with a data source which
        // finds nothing -- the very thing being greyed out elsewhere is meant to prevent.
        //
        var unsearchableIds = (await this.GetDataSourcesAwaitingReindex(allowedDataSources)).Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        unsearchableIds.UnionWith(this.GetDataSourcesNeedingRepair(allowedDataSources).Select(source => source.Id));
        return allowedDataSources.Where(source => !unsearchableIds.Contains(source.Id)).ToList();
    }
    
    /// <summary>
    /// Returns a list of data sources that are allowed for the selected LLM provider.
    /// It also returns the data sources selected before when they are still allowed.
    /// </summary>
    /// <param name="selectedLLMProvider">The selected LLM provider.</param>
    /// <param name="dataSourceOptions">The active data source options, which determine which agent providers participate.</param>
    /// <param name="retrievalMode">How the data sources are searched in effect, which decides whether any agent participates at all.</param>
    /// <param name="previousSelectedDataSources">The data sources selected before.</param>
    /// <returns>The allowed data sources and the data sources selected before -- when they are still allowed.</returns>
    public async Task<AllowedSelectedDataSources> GetDataSources(IProvider selectedLLMProvider, DataSourceOptions dataSourceOptions, DataSourceRetrievalMode retrievalMode, IReadOnlyCollection<IDataSource>? previousSelectedDataSources = null)
    {
        //
        // Case: Somehow the selected LLM provider was not set. The default provider
        //       does not mean anything. We cannot filter the data sources by any means.
        //       We return an empty list. Better safe than sorry.
        //
        if (selectedLLMProvider is NoProvider)
        {
            this.logger.LogWarning("The selected LLM provider is the default provider. We cannot filter the data sources by any means.");
            return new([], [], [], []);
        }
        
        var usingTrustedProvider = selectedLLMProvider.IsTrustedForDataSourceSecurityChecks(this.settingsManager);
        var participatingProviders = this.GetParticipatingProviders(selectedLLMProvider.ConfiguredProviderId, dataSourceOptions, retrievalMode,
            new("chat provider", usingTrustedProvider, selectedLLMProvider.GetConfidenceLevel(this.settingsManager)));
        return await this.GetDataSources(usingTrustedProvider, participatingProviders, previousSelectedDataSources);
    }
    
    private IReadOnlyList<ParticipatingProvider> GetParticipatingProviders(string currentProviderId, DataSourceOptions dataSourceOptions, DataSourceRetrievalMode retrievalMode, ParticipatingProvider currentProvider)
    {
        var providers = new List<ParticipatingProvider> { currentProvider };
        var retrievalContextValidationEnabled = this.settingsManager.ConfigurationData.AgentRetrievalContextValidation.EnableRetrievalContextValidation;
        foreach (var (component, role) in GetParticipatingAgents(dataSourceOptions, retrievalMode, retrievalContextValidationEnabled))
            this.AddAgentProvider(providers, component, currentProviderId, role);

        return providers;
    }

    /// <summary>
    /// Which agents get to see the data of the data sources, besides the chat provider.
    /// </summary>
    /// <remarks>
    /// Only the classic RAG process runs agents. With Semantic Search, the chat model picks the
    /// data sources and judges what it found itself, so the data reaches no other provider.
    /// Counting the providers of the agents there would hold back data sources which the chat
    /// provider alone may use.
    /// </remarks>
    /// <param name="dataSourceOptions">The active data source options.</param>
    /// <param name="retrievalMode">How the data sources are searched in effect.</param>
    /// <param name="retrievalContextValidationEnabled">Whether the validation of retrieval contexts is enabled in the settings.</param>
    /// <returns>The component of each participating agent, together with its role for the log.</returns>
    internal static IReadOnlyList<(Components Component, string Role)> GetParticipatingAgents(DataSourceOptions dataSourceOptions, DataSourceRetrievalMode retrievalMode, bool retrievalContextValidationEnabled)
    {
        if (retrievalMode is DataSourceRetrievalMode.SEMANTIC_SEARCH)
            return [];

        var agents = new List<(Components Component, string Role)>(2);
        if (dataSourceOptions.AutomaticDataSourceSelection)
            agents.Add((Components.AGENT_DATA_SOURCE_SELECTION, "data source selection agent"));

        if (dataSourceOptions.AutomaticValidation && retrievalContextValidationEnabled)
            agents.Add((Components.AGENT_RETRIEVAL_CONTEXT_VALIDATION, "retrieval context validation agent"));

        return agents;
    }

    private void AddAgentProvider(List<ParticipatingProvider> providers, Components component, string currentProviderId, string role)
    {
        var provider = this.settingsManager.GetPreselectedProvider(component, currentProviderId, true);
        if (provider == Settings.Provider.NONE)
        {
            this.logger.LogWarning($"No provider is available for the {role}. Data sources cannot be made available while this agent is enabled.");
            providers.Add(new(role, false, ConfidenceLevel.NONE));
            return;
        }

        providers.Add(new(
            role,
            provider.IsTrustedForDataSourceSecurityChecks(this.settingsManager),
            provider.GetConfidenceLevel(this.settingsManager)));
    }

    private async Task<AllowedSelectedDataSources> GetDataSources(bool usingTrustedProvider, IReadOnlyList<ParticipatingProvider> participatingProviders, IReadOnlyCollection<IDataSource>? previousSelectedDataSources = null)
    {
        var allDataSources = this.settingsManager.ConfigurationData.DataSources.ToList();
        var previousSelectedDataSourceIds = previousSelectedDataSources?.Select(source => source.Id).ToHashSet(StringComparer.Ordinal) ?? [];
        var filteredDataSources = await this.GetAllowedDataSources(usingTrustedProvider, participatingProviders, allDataSources);

        //
        // Which of the sources that passed every check cannot answer a search right now. They are
        // held back from both lists below rather than removed altogether: a source whose index is
        // being rebuilt is usable again in a while, and saying so on its own row beats letting it
        // disappear from the selection without a word.
        //
        // A source whose index cannot be read is asked about first and then kept out of the other
        // list: both reasons can be true at once, and of the two it is the only one the user can do
        // anything about. Telling them to wait instead would be telling them to wait forever.
        //
        var needingRepair = this.GetDataSourcesNeedingRepair(filteredDataSources);
        var needingRepairIds = needingRepair.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        var awaitingReindex = (await this.GetDataSourcesAwaitingReindex(filteredDataSources)).Where(source => !needingRepairIds.Contains(source.Id)).ToList();

        var blockedIds = awaitingReindex.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        blockedIds.UnionWith(needingRepairIds);

        var usableDataSources = filteredDataSources.Where(source => !blockedIds.Contains(source.Id)).ToList();
        var filteredSelectedDataSources = usableDataSources.Where(source => previousSelectedDataSourceIds.Contains(source.Id)).ToList();

        return new(usableDataSources, filteredSelectedDataSources, awaitingReindex, needingRepair);
    }

    /// <summary>
    /// Picks out the data sources whose index has to be rebuilt before they can be searched.
    /// </summary>
    /// <remarks>
    /// Asked for every data source at once, the same way the checks above run in parallel. Each
    /// answer is a single row read from the index database, and anything unclear counts as usable.
    /// </remarks>
    /// <param name="dataSources">The data sources which passed every other check.</param>
    /// <returns>Those of them which are waiting for their index, in the order they came in.</returns>
    private async Task<IReadOnlyList<IDataSource>> GetDataSourcesAwaitingReindex(IReadOnlyList<IDataSource> dataSources)
    {
        var checks = new List<Task<bool>>(dataSources.Count);
        foreach (var dataSource in dataSources)
            checks.Add(this.embeddingService.IsAwaitingReindexAsync(dataSource));

        var awaitingReindex = new List<IDataSource>();
        for (var index = 0; index < dataSources.Count; index++)
        {
            if (await checks[index])
            {
                this.logger.LogInformation("The data source '{DataSourceName}' ({DataSourceId}) is waiting for its index to be rebuilt. It is shown, but cannot be selected.", dataSources[index].Name, dataSources[index].Id);
                awaitingReindex.Add(dataSources[index]);
            }
        }

        return awaitingReindex;
    }

    /// <summary>
    /// Picks out the data sources whose index cannot be read anymore, so that they wait for a repair.
    /// </summary>
    /// <remarks>
    /// Reads nothing from a database, unlike the re-index check above: the state is held in memory
    /// by the embedding service, which is why this one needs no parallelism and no timeout.
    /// </remarks>
    /// <param name="dataSources">The data sources which passed every other check.</param>
    /// <returns>Those of them which wait for a repair, in the order they came in.</returns>
    private IReadOnlyList<IDataSource> GetDataSourcesNeedingRepair(IReadOnlyList<IDataSource> dataSources)
    {
        var needingRepair = new List<IDataSource>();
        foreach (var dataSource in dataSources)
        {
            if (!this.embeddingService.NeedsIndexRepair(dataSource))
                continue;

            this.logger.LogInformation("The index of data source '{DataSourceName}' ({DataSourceId}) cannot be read. It is shown, but cannot be selected until it was repaired.", dataSource.Name, dataSource.Id);
            needingRepair.Add(dataSource);
        }

        return needingRepair;
    }

    private async Task<IReadOnlyList<IDataSource>> GetAllowedDataSources(bool usingTrustedProvider, IReadOnlyList<ParticipatingProvider> participatingProviders, IReadOnlyCollection<IDataSource> requestedDataSources)
    {
        var filteredDataSources = new List<IDataSource>(requestedDataSources.Count);
        var tasks = new List<Task<IDataSource?>>(requestedDataSources.Count);

        // Start all checks in parallel:
        foreach (var source in requestedDataSources)
            tasks.Add(this.CheckOneDataSource(source, usingTrustedProvider, participatingProviders));


        // Wait for all checks and collect the results:
        foreach (var task in tasks)
        {
            var source = await task;
            if (source is not null)
                filteredDataSources.Add(source);
        }

        return filteredDataSources;
    }
    
    private async Task<IDataSource?> CheckOneDataSource(IDataSource source, bool usingTrustedProvider, IReadOnlyList<ParticipatingProvider> participatingProviders)
    {
        if (source is IInternalDataSource internalSource)
        {
            foreach (var provider in participatingProviders)
            {
                if (!provider.ConfidenceLevel.AllowsDataSourceConfidenceLevel(internalSource.ConfidenceLevel))
                {
                    this.logger.LogWarning($"The internal data source '{source.Name}' (id={source.Id}) requires provider confidence '{internalSource.ConfidenceLevel.GetName()}'. The {provider.Role} only has confidence '{provider.ConfidenceLevel.GetName()}'. We skip this source.");
                    return null;
                }
            }

            if (!DataSourceEmbeddingProviders.TryResolve(this.settingsManager, source, out var embeddingProvider))
            {
                this.logger.LogWarning($"The internal data source '{source.Name}' (id={source.Id}) has no usable embedding provider. We skip this source.");
                return null;
            }

            var embeddingProviderConfidence = embeddingProvider.GetConfidenceLevel(this.settingsManager);
            if (!embeddingProviderConfidence.AllowsDataSourceConfidenceLevel(internalSource.ConfidenceLevel))
            {
                this.logger.LogWarning($"The internal data source '{source.Name}' (id={source.Id}) requires provider confidence '{internalSource.ConfidenceLevel.GetName()}'. Its embedding provider '{embeddingProvider.Name}' only has confidence '{embeddingProviderConfidence.GetName()}'. We skip this source.");
                return null;
            }

            return source;
        }

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

        if (source is not IExternalDataSource externalSource)
            return source;

        switch (externalSource.SecurityPolicy)
        {
            case DataSourceSecurity.ALLOW_ANY:
                
                //
                // Case: The data source allows any provider type. We want to use a trusted provider.
                //       There is no issue with this source. Accept it.
                //
                if(usingTrustedProvider)
                    return source;

                //
                // Case: This is a local data source. When the source allows any provider type, we can use it.
                //       Accept it.
                //
                if(eriSourceRequirements is null)
                    return source;

                //
                // Case: The ERI source requires a self-hosted or organization-trusted provider. This misconfiguration happens
                //       when the ERI server operator changes the security requirements. The ERI server
                //       operator owns the data -- we have to respect their rules. We skip this source.
                //
                if (eriSourceRequirements is { AllowedProviderType: ProviderType.SELF_HOSTED })
                {
                    this.logger.LogWarning($"The ERI source '{source.Name}' (id={source.Id}) requires a self-hosted or organization-trusted provider. We skip this source.");
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
                this.logger.LogWarning($"The ERI source '{source.Name}' (id={source.Id}) was filtered out due to missing rules.");
                return null;

            //
            // Case: The data source requires a trusted provider. We want to use a trusted provider.
            //       There is no issue with this source. Accept it.
            //
            case DataSourceSecurity.SELF_HOSTED when usingTrustedProvider:
                return source;
            
            //
            // Case: The data source requires a trusted provider. We want to use an untrusted provider.
            //       We skip this source.
            //
            case DataSourceSecurity.SELF_HOSTED when !usingTrustedProvider:
                this.logger.LogWarning($"The data source '{source.Name}' (id={source.Id}) requires a self-hosted or organization-trusted provider. We skip this source.");
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
