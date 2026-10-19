using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.UserAPIKeys;

/// <summary>
/// Finds the providers which a configuration plugin marked with "PromptForUserAPIKey" and which have no API key yet,
/// and stores the API key the user enters for them.
/// </summary>
public static class PendingUserAPIKeys
{
    /// <summary>
    /// Finds all providers which still wait for the API key of the user, grouped by their configuration plugin.
    /// </summary>
    /// <param name="settingsManager">The settings manager, which knows the configured providers.</param>
    /// <param name="rustService">The Rust service, which knows the stored API keys.</param>
    /// <returns>One group per configuration plugin with at least one waiting provider.</returns>
    public static async Task<IReadOnlyList<PendingUserAPIKeyGroup>> FindAsync(SettingsManager settingsManager, RustService rustService)
    {
        var candidates = new List<(Guid ConfigPluginId, PendingUserAPIKeyTarget Target)>();
        foreach (var provider in settingsManager.GetAllProviders().Where(provider => IsPrompting(provider)))
            candidates.Add((provider.EnterpriseConfigurationPluginId, new PendingUserAPIKeyTarget(provider.InstanceName, provider, SecretStoreType.LLM_PROVIDER, () => provider.CreateProvider())));

        foreach (var provider in settingsManager.GetAllEmbeddingProviders().Where(provider => IsPrompting(provider)))
            candidates.Add((provider.EnterpriseConfigurationPluginId, new PendingUserAPIKeyTarget(provider.Name, provider, SecretStoreType.EMBEDDING_PROVIDER, () => provider.CreateProvider())));

        foreach (var provider in settingsManager.GetAllTranscriptionProviders().Where(provider => IsPrompting(provider)))
            candidates.Add((provider.EnterpriseConfigurationPluginId, new PendingUserAPIKeyTarget(provider.Name, provider, SecretStoreType.TRANSCRIPTION_PROVIDER, () => provider.CreateProvider())));

        if (candidates.Count == 0)
            return [];

        var pending = new List<(Guid ConfigPluginId, PendingUserAPIKeyTarget Target)>();
        foreach (var candidate in candidates)
        {
            var storedKey = await rustService.GetAPIKey(candidate.Target.SecretId, candidate.Target.StoreType, isTrying: true);
            if (!storedKey.Success)
                pending.Add(candidate);
        }

        if (pending.Count == 0)
            return [];

        var prompts = PluginFactory.GetUserAPIKeyPrompts().ToDictionary(prompt => prompt.EnterpriseConfigurationPluginId);
        return pending
            .GroupBy(entry => entry.ConfigPluginId)
            .Select(group => new PendingUserAPIKeyGroup(group.Key, prompts.GetValueOrDefault(group.Key), group.Select(entry => entry.Target).ToList()))
            .ToList();
    }

    /// <summary>
    /// Checks the API key the user entered with one of the providers of a group, and stores it for all of them
    /// unless the provider refused it.
    /// </summary>
    /// <remarks>
    /// The providers of a group share one key, so asking one of them is enough: the first which can list its models.
    /// When none of them can, or the answer tells nothing about the key, for example because the server is out of
    /// reach, AI Studio stores the key anyway. Otherwise, an outage would keep the user from finishing the setup.
    /// </remarks>
    /// <param name="group">The providers which receive the key.</param>
    /// <param name="apiKey">The API key the user entered.</param>
    /// <param name="rustService">The Rust service, which stores the key in the OS keyring.</param>
    /// <param name="logger">The logger for refused keys and failures to store one.</param>
    /// <returns>What the provider said about the key, and for which providers storing it failed.</returns>
    public static async Task<UserAPIKeyStoreResult> StoreAsync(PendingUserAPIKeyGroup group, string apiKey, RustService rustService, ILogger logger)
    {
        var check = await CheckAsync(group, apiKey, logger);
        if (check is UserAPIKeyCheckOutcome.REJECTED)
            return UserAPIKeyStoreResult.REJECTED;

        var failedProviderNames = new List<string>();
        foreach (var target in group.Targets)
        {
            var storeResponse = await rustService.SetAPIKey(target.SecretId, apiKey, target.StoreType);
            if (storeResponse.Success)
                continue;

            logger.LogError("Failed to store the API key for the provider '{ProviderName}': {Issue}", target.Name, storeResponse.Issue);
            failedProviderNames.Add(target.Name);
        }

        return new UserAPIKeyStoreResult(check, failedProviderNames);
    }

    /// <summary>
    /// Tells from the models a provider loaded with an API key what the provider says about that key.
    /// </summary>
    /// <remarks>
    /// Only a refused sign-in rejects the key, and only at least one model accepts it. Everything else tells nothing
    /// about the key: an outage, a rate limit, a used-up quota, or an empty list, which some providers return for a
    /// kind of model they do not offer, whatever the key. One gap remains: a few providers, such as Perplexity, return
    /// a fixed list without asking their server, so any key passes there.
    /// </remarks>
    /// <param name="result">The models the provider loaded with the key.</param>
    /// <returns>What the provider says about the key.</returns>
    public static UserAPIKeyCheckOutcome ClassifyCheck(ModelLoadResult result) => result switch
    {
        { FailureReason: ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY or ModelLoadFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR } => UserAPIKeyCheckOutcome.REJECTED,
        { Success: true, Models.Count: > 0 } => UserAPIKeyCheckOutcome.ACCEPTED,
        _ => UserAPIKeyCheckOutcome.NOT_CHECKED
    };

    private static async Task<UserAPIKeyCheckOutcome> CheckAsync(PendingUserAPIKeyGroup group, string apiKey, ILogger logger)
    {
        //
        // Every provider instance brings an HTTP client of its own. Hence, we create one only here,
        // and only until we found a provider which can answer:
        //
        foreach (var target in group.Targets)
        {
            var provider = target.CreateProvider();
            if (!provider.HasModelLoadingCapability)
                continue;

            var outcome = ClassifyCheck(await LoadModelsAsync(provider, target.StoreType, apiKey));
            if (outcome is UserAPIKeyCheckOutcome.REJECTED)
                logger.LogWarning("The provider '{ProviderName}' rejected the API key the user entered.", target.Name);

            return outcome;
        }

        return UserAPIKeyCheckOutcome.NOT_CHECKED;
    }

    private static Task<ModelLoadResult> LoadModelsAsync(IProvider provider, SecretStoreType storeType, string apiKey) => storeType switch
    {
        SecretStoreType.LLM_PROVIDER => provider.GetTextModels(apiKey),
        SecretStoreType.EMBEDDING_PROVIDER => provider.GetEmbeddingModels(apiKey),
        SecretStoreType.TRANSCRIPTION_PROVIDER => provider.GetTranscriptionModels(apiKey),
        _ => throw new ArgumentOutOfRangeException(nameof(storeType), storeType, "Only providers wait for the API key of the user.")
    };

    private static bool IsPrompting<T>(T provider) where T : IConfigurationObject, IUserProvidedAPIKey => provider is { IsEnterpriseConfiguration: true, PromptForUserAPIKey: true };
}