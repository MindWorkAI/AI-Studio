using System.Globalization;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

using Lua;

namespace AIStudio.Tools.PluginSystem;

public sealed class PluginConfiguration(bool isInternal, LuaState state, PluginType type) : PluginBase(isInternal, state, type)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginConfiguration).Namespace, nameof(PluginConfiguration));
    private static SettingsManager SettingsManagerAccess => Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(PluginConfiguration));

    private List<PluginConfigurationObject> configObjects = [];
    private List<DataMandatoryInfo> mandatoryInfos = [];
    private List<DataIntroduction> introductions = [];
    
    /// <summary>
    /// The list of configuration objects. Configuration objects are, e.g., providers or chat templates. 
    /// </summary>
    public IEnumerable<PluginConfigurationObject> ConfigObjects => this.configObjects;

    /// <summary>
    /// The list of mandatory infos provided by this configuration plugin.
    /// Mandatory infos are live plugin content and are not persisted to ConfigurationData.
    /// </summary>
    public IReadOnlyList<DataMandatoryInfo> MandatoryInfos => this.mandatoryInfos;

    /// <summary>
    /// The list of introductions provided by this configuration plugin.
    /// Introductions are live plugin content and are not persisted to ConfigurationData.
    /// </summary>
    public IReadOnlyList<DataIntroduction> Introductions => this.introductions;

    /// <summary>
    /// True/false when explicitly configured in the plugin, otherwise null.
    /// </summary>
    public bool? DeployedUsingConfigServer { get; } = ReadDeployedUsingConfigServer(state);
    
    public async Task InitializeAsync(bool dryRun)
    {
        if(!this.TryProcessConfiguration(dryRun, out var issue))
            this.PluginIssues.Add(issue);

        if (!dryRun)
        {
            // Store any decrypted API keys from enterprise configuration in the OS keyring:
            await StoreEnterpriseApiKeysAsync();
            await StoreEnterpriseSecretsAsync();

            await SettingsManagerAccess.StoreSettings();
            await MessageBus.INSTANCE.SendMessage<bool>(null, Event.CONFIGURATION_CHANGED);
        }
    }

    /// <summary>
    /// Stores any pending enterprise secrets in the OS keyring.
    /// </summary>
    private static async Task StoreEnterpriseSecretsAsync()
    {
        var pendingSecrets = PendingEnterpriseSecrets.GetAndClear();
        if (pendingSecrets.Count == 0)
            return;

        LOG.LogInformation($"Storing {pendingSecrets.Count} enterprise secret(s) in the OS keyring.");
        var rustService = Program.SERVICE_PROVIDER.GetRequiredService<RustService>();
        foreach (var pendingSecret in pendingSecrets)
        {
            try
            {
                var secretId = new TemporarySecretId(pendingSecret.SecretId, pendingSecret.SecretName);
                var result = await rustService.SetSecret(secretId, pendingSecret.SecretData, pendingSecret.StoreType);

                if (result.Success)
                    LOG.LogDebug($"Successfully stored enterprise secret for '{pendingSecret.SecretName}' in the OS keyring.");
                else
                    LOG.LogWarning($"Failed to store enterprise secret for '{pendingSecret.SecretName}': {result.Issue}");
            }
            catch (Exception ex)
            {
                LOG.LogError(ex, $"Exception while storing enterprise secret for '{pendingSecret.SecretName}'.");
            }
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

    private static bool? ReadDeployedUsingConfigServer(LuaState state)
    {
        if (state.Environment["DEPLOYED_USING_CONFIG_SERVER"].TryRead<bool>(out var deployedUsingConfigServer))
            return deployedUsingConfigServer;

        return null;
    }

    /// <summary>
    /// Tries to initialize the UI text content of the plugin.
    /// </summary>
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <param name="message">The error message, when the UI text content could not be read.</param>
    /// <returns>True, when the UI text content could be read successfully.</returns>
    private bool TryProcessConfiguration(bool dryRun, out string message)
    {
        this.configObjects.Clear();
        this.mandatoryInfos.Clear();
        this.introductions.Clear();
        
        // Ensure that the main CONFIG table exists and is a valid Lua table:
        if (!this.State.Environment["CONFIG"].TryRead<LuaTable>(out var mainTable))
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

        // Config: what should be the start page?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.StartPage, this.Id, settingsTable, dryRun);

        // Config: show built-in introduction on the home page?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShowIntroduction, this.Id, settingsTable, dryRun);

        // Config: show quick start guide on the home page?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShowQuickStartGuide, this.Id, settingsTable, dryRun);
        
        // Config: allow the user to add providers?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.AllowUserToAddProvider, this.Id, settingsTable, dryRun);

        // Config: show administration settings?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShowAdminSettings, this.Id, settingsTable, dryRun);
        
        // Config: preview features visibility
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.PreviewVisibility, this.Id, settingsTable, dryRun);
        
        // Config: enabled preview features (plugin contribution; users can enable additional features)
        ManagedConfiguration.TryProcessConfigurationWithPluginContribution(x => x.App, x => x.EnabledPreviewFeatures, this.Id, settingsTable, dryRun);
        
        // Config: hide some assistants?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.HiddenAssistants, this.Id, settingsTable, dryRun);
        
        // Config: global voice recording shortcut
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShortcutVoiceRecording, this.Id, settingsTable, dryRun);

        // Config: timeout for external HTTP requests
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.HttpClientTimeoutSeconds, this.Id, settingsTable, dryRun);

        // Config: custom root certificates for external HTTP requests
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ExternalHttpCustomRootCertificatesEnabled, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ExternalHttpCustomRootCertificateBundlePath, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ExternalHttpCustomRootCertificateAllowedHosts, this.Id, settingsTable, dryRun);

        // Config: provider confidence settings
        ManagedConfiguration.TryProcessConfiguration(x => x.Confidence, x => x.EnforceGlobalMinimumConfidence, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Confidence, x => x.GlobalMinimumConfidence, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Confidence, x => x.ShowProviderConfidence, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Confidence, x => x.ConfidenceScheme, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Confidence, x => x.CustomConfidenceScheme, this.Id, settingsTable, dryRun);

        // Config: data source security settings
        ManagedConfiguration.TryProcessConfiguration(x => x.DataSourceSecurity, x => x.TrustedProviderIds, this.Id, settingsTable, dryRun);

        // Config: enterprise-managed approvals for assistant plugins
        this.TryProcessEnterpriseApprovedAssistantPlugins(settingsTable, dryRun);
        
        // Handle configured LLM providers:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.LLM_PROVIDER, x => x.Providers, x => x.NextProviderNum, mainTable, this.Id, ref this.configObjects, dryRun);

        // Handle configured transcription providers:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER, x => x.TranscriptionProviders, x => x.NextTranscriptionNum, mainTable, this.Id, ref this.configObjects, dryRun);

        // Handle configured embedding providers:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.EMBEDDING_PROVIDER, x => x.EmbeddingProviders, x => x.NextEmbeddingNum, mainTable, this.Id, ref this.configObjects, dryRun);

        // Handle configured chat templates:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.CHAT_TEMPLATE, x => x.ChatTemplates, x => x.NextChatTemplateNum, mainTable, this.Id, ref this.configObjects, dryRun, this.PluginPath);

        // Handle configured data sources:
        PluginConfigurationObject.TryParseDataSources(mainTable, this.Id, ref this.configObjects, dryRun);
        
        // Handle configured profiles:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.PROFILE, x => x.Profiles, x => x.NextProfileNum, mainTable, this.Id, ref this.configObjects, dryRun);
        
        // Handle configured document analysis policies:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY, x => x.DocumentAnalysis.Policies, x => x.NextDocumentAnalysisPolicyNum, mainTable, this.Id, ref this.configObjects, dryRun);

        // Handle configured mandatory infos:
        this.TryReadMandatoryInfos(mainTable);

        // Handle configured introductions:
        this.TryReadIntroductions(mainTable);
        
        // Config: preselected provider?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.PreselectedProvider, Guid.Empty, this.Id, settingsTable, dryRun);

        // Config: preselected profile?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.PreselectedProfile, Guid.Empty, this.Id, settingsTable, dryRun);

        // Config: preselected chat options?
        ManagedConfiguration.TryProcessConfiguration(x => x.Chat, x => x.PreselectOptions, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Chat, x => x.PreselectedProvider, Guid.Empty, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Chat, x => x.PreselectedProfile, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Chat, x => x.PreselectedChatTemplate, this.Id, settingsTable, dryRun);

        // Config: transcription provider?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UseTranscriptionProvider, Guid.Empty, this.Id, settingsTable, dryRun);

        message = string.Empty;
        return true;
    }

    private void TryProcessEnterpriseApprovedAssistantPlugins(LuaTable settingsTable, bool dryRun)
    {
        if (!ManagedConfiguration.TryGet(x => x.AssistantPluginAudit, x => x.EnterpriseApprovedPlugins, out ConfigMeta<DataAssistantPluginAudit, IList<DataAssistantPluginEnterpriseApproval>> configMeta))
            return;

        var settingName = SettingsManager.ToSettingName<DataAssistantPluginAudit, IList<DataAssistantPluginEnterpriseApproval>>(x => x.EnterpriseApprovedPlugins);
        var successful = false;
        IList<DataAssistantPluginEnterpriseApproval> configuredApprovals = [];

        if (settingsTable.TryGetValue(settingName, out var configuredLuaValue)
            && configuredLuaValue.Type is LuaValueType.Table
            && configuredLuaValue.TryRead<LuaTable>(out var approvalsTable))
        {
            var approvals = new List<DataAssistantPluginEnterpriseApproval>(approvalsTable.ArrayLength);
            for (var index = 1; index <= approvalsTable.ArrayLength; index++)
            {
                var entryValue = approvalsTable[index];
                if (entryValue.TryRead<string>(out var hashText))
                {
                    var normalizedHash = NormalizeApprovalHash(hashText);
                    if (!string.IsNullOrWhiteSpace(normalizedHash))
                        approvals.Add(new() { PluginHash = normalizedHash });
                    else
                        LOG.LogWarning("The enterprise assistant approval entry at index {Index} contains an empty hash (config plugin id: {ConfigPluginId}).", index, this.Id);

                    continue;
                }

                if (!entryValue.TryRead<LuaTable>(out var entryTable))
                {
                    LOG.LogWarning("The enterprise assistant approval entry at index {Index} is neither a string nor a table (config plugin id: {ConfigPluginId}).", index, this.Id);
                    continue;
                }

                if (!TryParseEnterpriseApprovedAssistantPlugin(index, entryTable, this.Id, out var approval))
                    continue;

                approvals.Add(approval);
            }

            configuredApprovals = approvals;
            successful = true;
        }

        if (dryRun)
            return;

        switch (successful)
        {
            case true:
                configMeta.SetValue(configuredApprovals);
                configMeta.LockConfiguration(this.Id);
                break;

            case false when configMeta.IsLocked && configMeta.LockedByConfigPluginId == this.Id:
                configMeta.ResetLockedConfiguration();
                break;
        }
    }

    private static bool TryParseEnterpriseApprovedAssistantPlugin(int index, LuaTable table, Guid configPluginId, out DataAssistantPluginEnterpriseApproval approval)
    {
        approval = new();

        if (!table.TryGetValue("PluginHash", out var pluginHashValue) || !pluginHashValue.TryRead<string>(out var pluginHash))
        {
            LOG.LogWarning("The enterprise assistant approval entry at index {Index} is missing a valid PluginHash (config plugin id: {ConfigPluginId}).", index, configPluginId);
            return false;
        }

        var normalizedHash = NormalizeApprovalHash(pluginHash);
        if (string.IsNullOrWhiteSpace(normalizedHash))
        {
            LOG.LogWarning("The enterprise assistant approval entry at index {Index} contains an empty PluginHash (config plugin id: {ConfigPluginId}).", index, configPluginId);
            return false;
        }

        var displayName = TryReadOptionalString(table, "DisplayName");
        var comment = TryReadOptionalString(table, "Comment");
        var approvedBy = TryReadOptionalString(table, "ApprovedBy");
        var approvedAtUtc = TryReadOptionalDateTimeOffset(table, "ApprovedAtUtc", index, configPluginId);

        approval = new()
        {
            PluginHash = normalizedHash,
            DisplayName = displayName,
            Comment = comment,
            ApprovedBy = approvedBy,
            ApprovedAtUtc = approvedAtUtc,
        };
        return true;
    }

    private static string TryReadOptionalString(LuaTable table, string key)
    {
        return table.TryGetValue(key, out var value) && value.TryRead<string>(out var text)
            ? text
            : string.Empty;
    }

    private static DateTimeOffset? TryReadOptionalDateTimeOffset(LuaTable table, string key, int index, Guid configPluginId)
    {
        if (!table.TryGetValue(key, out var value))
            return null;

        if (value.TryRead<string>(out var text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.ToUniversalTime();

        LOG.LogWarning("The enterprise assistant approval entry at index {Index} contains an invalid {Key} value (config plugin id: {ConfigPluginId}).", index, key, configPluginId);
        return null;
    }

    private static string NormalizeApprovalHash(string hash) => string.IsNullOrWhiteSpace(hash) ? string.Empty : hash.Trim().ToUpperInvariant();

    private void TryReadMandatoryInfos(LuaTable mainTable)
    {
        if (!mainTable.TryGetValue("MANDATORY_INFOS", out var mandatoryInfosValue) || !mandatoryInfosValue.TryRead<LuaTable>(out var mandatoryInfosTable))
            return;

        for (var i = 1; i <= mandatoryInfosTable.ArrayLength; i++)
        {
            var luaMandatoryInfoValue = mandatoryInfosTable[i];
            if (!luaMandatoryInfoValue.TryRead<LuaTable>(out var luaMandatoryInfoTable))
            {
                LOG.LogWarning("The table 'MANDATORY_INFOS' entry at index {Index} is not a valid table (config plugin id: {ConfigPluginId}).", i, this.Id);
                continue;
            }

            if (DataMandatoryInfo.TryParseConfiguration(i, luaMandatoryInfoTable, this.Id, out var mandatoryInfo))
                this.mandatoryInfos.Add(mandatoryInfo);
            else
                LOG.LogWarning("The table 'MANDATORY_INFOS' entry at index {Index} does not contain a valid mandatory info (config plugin id: {ConfigPluginId}).", i, this.Id);
        }
    }

    private void TryReadIntroductions(LuaTable mainTable)
    {
        if (!mainTable.TryGetValue("INTRODUCTIONS", out var introductionsValue) || !introductionsValue.TryRead<LuaTable>(out var introductionsTable))
            return;

        for (var i = 1; i <= introductionsTable.ArrayLength; i++)
        {
            var luaIntroductionValue = introductionsTable[i];
            if (!luaIntroductionValue.TryRead<LuaTable>(out var luaIntroductionTable))
            {
                LOG.LogWarning("The table 'INTRODUCTIONS' entry at index {Index} is not a valid table (config plugin id: {ConfigPluginId}).", i, this.Id);
                continue;
            }

            if (DataIntroduction.TryParseConfiguration(i, luaIntroductionTable, this.Id, out var introduction))
                this.introductions.Add(introduction);
            else
                LOG.LogWarning("The table 'INTRODUCTIONS' entry at index {Index} does not contain a valid introduction (config plugin id: {ConfigPluginId}).", i, this.Id);
        }
    }
}
