using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools;

/// <summary>
/// One provider of a configuration plugin which waits for the API key of the user.
/// </summary>
/// <param name="Name">The name of the provider as the user knows it.</param>
/// <param name="SecretId">Where the API key of the provider is stored.</param>
/// <param name="StoreType">The secret store of the provider's kind.</param>
/// <param name="LoadModels">Loads the models of the provider with a provisional API key, which tells whether the provider accepts that key.</param>
public sealed record PendingUserAPIKeyTarget(string Name, ISecretId SecretId, SecretStoreType StoreType, Func<string, Task<ModelLoadResult>> LoadModels);

/// <summary>
/// The providers of one configuration plugin which wait for the API key of the user. They share one key.
/// </summary>
/// <param name="ConfigPluginId">The ID of the configuration plugin.</param>
/// <param name="Prompt">What the configuration plugin tells the user about obtaining the key, if anything.</param>
/// <param name="Targets">The providers which receive the key.</param>
public sealed record PendingUserAPIKeyGroup(Guid ConfigPluginId, DataUserAPIKeyPrompt? Prompt, IReadOnlyList<PendingUserAPIKeyTarget> Targets);

/// <summary>
/// Finds the providers which a configuration plugin marked with "PromptForUserAPIKey" and which have no API key yet.
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
            candidates.Add((provider.EnterpriseConfigurationPluginId, new(provider.InstanceName, provider, SecretStoreType.LLM_PROVIDER, apiKey => provider.CreateProvider().GetTextModels(apiKey))));

        foreach (var provider in settingsManager.GetAllEmbeddingProviders().Where(provider => IsPrompting(provider)))
            candidates.Add((provider.EnterpriseConfigurationPluginId, new(provider.Name, provider, SecretStoreType.EMBEDDING_PROVIDER, apiKey => provider.CreateProvider().GetEmbeddingModels(apiKey))));

        foreach (var provider in settingsManager.GetAllTranscriptionProviders().Where(provider => IsPrompting(provider)))
            candidates.Add((provider.EnterpriseConfigurationPluginId, new(provider.Name, provider, SecretStoreType.TRANSCRIPTION_PROVIDER, apiKey => provider.CreateProvider().GetTranscriptionModels(apiKey))));

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

    private static bool IsPrompting<T>(T provider) where T : IConfigurationObject, IUserProvidedAPIKey => provider is { IsEnterpriseConfiguration: true, PromptForUserAPIKey: true };
}
