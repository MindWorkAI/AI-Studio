using AIStudio.Settings;
using AIStudio.Tools.Services;

using Lua;

namespace AIStudio.Tools.PluginSystem;

public sealed class PluginConfiguration(bool isInternal, LuaState state, PluginType type) : PluginBase(isInternal, state, type)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginConfiguration).Namespace, nameof(PluginConfiguration));
    private static readonly SettingsManager SETTINGS_MANAGER = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(PluginConfiguration));

    private List<PluginConfigurationObject> configObjects = [];
    
    /// <summary>
    /// The list of configuration objects. Configuration objects are, e.g., providers or chat templates. 
    /// </summary>
    public IEnumerable<PluginConfigurationObject> ConfigObjects => this.configObjects;
    
    public async Task InitializeAsync(bool dryRun)
    {
        if(!this.TryProcessConfiguration(dryRun, out var issue))
            this.pluginIssues.Add(issue);

        if (!dryRun)
        {
            // Store any decrypted API keys from enterprise configuration in the OS keyring:
            await StoreEnterpriseApiKeysAsync();

            await SETTINGS_MANAGER.StoreSettings();
            await MessageBus.INSTANCE.SendMessage<bool>(null, Event.CONFIGURATION_CHANGED);
        }
    }

    /// <summary>
    /// Stores any pending enterprise API keys in the OS keyring.
    /// </summary>
    private static async Task StoreEnterpriseApiKeysAsync()
    {
        var pendingKeys = PendingEnterpriseApiKeys.GetAndClear();
        if (pendingKeys.Count == 0)
            return;

        LOG.LogInformation($"Storing {pendingKeys.Count} enterprise API key(s) in the OS keyring.");
        var rustService = Program.SERVICE_PROVIDER.GetRequiredService<RustService>();
        foreach (var pendingKey in pendingKeys)
        {
            try
            {
                // Create a temporary secret ID object for storing the key:
                var secretId = new TemporarySecretId(pendingKey.SecretId, pendingKey.SecretName);
                var result = await rustService.SetAPIKey(secretId, pendingKey.ApiKey, pendingKey.StoreType);

                if (result.Success)
                    LOG.LogDebug($"Successfully stored enterprise API key for '{pendingKey.SecretName}' in the OS keyring.");
                else
                    LOG.LogWarning($"Failed to store enterprise API key for '{pendingKey.SecretName}': {result.Issue}");
            }
            catch (Exception ex)
            {
                LOG.LogError(ex, $"Exception while storing enterprise API key for '{pendingKey.SecretName}'.");
            }
        }
    }

    /// <summary>
    /// Temporary implementation of ISecretId for storing enterprise API keys.
    /// </summary>
    private sealed record TemporarySecretId(string SecretId, string SecretName) : ISecretId;

    /// <summary>
    /// Tries to initialize the UI text content of the plugin.
    /// </summary>
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <param name="message">The error message, when the UI text content could not be read.</param>
    /// <returns>True, when the UI text content could be read successfully.</returns>
    private bool TryProcessConfiguration(bool dryRun, out string message)
    {
        this.configObjects.Clear();
        
        // Ensure that the main CONFIG table exists and is a valid Lua table:
        if (!this.state.Environment["CONFIG"].TryRead<LuaTable>(out var mainTable))
        {
            message = TB("The CONFIG table does not exist or is not a valid table.");
            return false;
        }
        
        // Check for the main SETTINGS table:
        if (!mainTable.TryGetValue("SETTINGS", out var settingsValue) || !settingsValue.TryRead<LuaTable>(out var settingsTable))
        {
            message = TB("The SETTINGS table does not exist or is not a valid table.");
            return false;
        }
        
        // Config: check for updates, and if so, how often?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UpdateInterval, this.Id, settingsTable, dryRun);
        
        // Config: how should updates be installed?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UpdateInstallation, this.Id, settingsTable, dryRun);
        
        // Config: allow the user to add providers?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.AllowUserToAddProvider, this.Id, settingsTable, dryRun);

        // Config: show administration settings?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShowAdminSettings, this.Id, settingsTable, dryRun);
        
        // Config: preview features visibility
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.PreviewVisibility, this.Id, settingsTable, dryRun);
        
        // Config: enabled preview features
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.EnabledPreviewFeatures, this.Id, settingsTable, dryRun);
        
        // Config: hide some assistants?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.HiddenAssistants, this.Id, settingsTable, dryRun);
        
        // Config: global voice recording shortcut
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShortcutVoiceRecording, this.Id, settingsTable, dryRun);
        
        // Handle configured LLM providers:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.LLM_PROVIDER, x => x.Providers, x => x.NextProviderNum, mainTable, this.Id, ref this.configObjects, dryRun);

        // Handle configured transcription providers:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER, x => x.TranscriptionProviders, x => x.NextTranscriptionNum, mainTable, this.Id, ref this.configObjects, dryRun);

        // Handle configured embedding providers:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.EMBEDDING_PROVIDER, x => x.EmbeddingProviders, x => x.NextEmbeddingNum, mainTable, this.Id, ref this.configObjects, dryRun);

        // Handle configured chat templates:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.CHAT_TEMPLATE, x => x.ChatTemplates, x => x.NextChatTemplateNum, mainTable, this.Id, ref this.configObjects, dryRun);
        
        // Handle configured profiles:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.PROFILE, x => x.Profiles, x => x.NextProfileNum, mainTable, this.Id, ref this.configObjects, dryRun);
        
        // Handle configured document analysis policies:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY, x => x.DocumentAnalysis.Policies, x => x.NextDocumentAnalysisPolicyNum, mainTable, this.Id, ref this.configObjects, dryRun);
        
        // Config: preselected profile?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.PreselectedProfile, Guid.Empty, this.Id, settingsTable, dryRun);

        // Config: transcription provider?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UseTranscriptionProvider, Guid.Empty, this.Id, settingsTable, dryRun);

        message = string.Empty;
        return true;
    }
}
