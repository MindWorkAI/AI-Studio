using System.Globalization;

using AIStudio.Provider;
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

    /// <summary>
    /// The priority of this configuration plugin. Defaults to zero when the plugin declares none.
    /// </summary>
    /// <remarks>
    /// Configuration plugins with a higher priority are applied later and therefore win when two of
    /// them manage the same setting or define the same configuration object. This lets an
    /// organization deploy one base configuration for everybody and additional configurations which
    /// refine it, e.g. per department.
    /// </remarks>
    public int Priority { get; } = ReadPriority(state);

    /// <summary>
    /// How many settings this configuration plugin declares.
    /// </summary>
    /// <remarks>
    /// This counts the entries of the Lua SETTINGS table, without the <c>.AllowUserOverride</c>
    /// companions. We need it for the import preview: a dry run does not lock anything, so the
    /// number of settings the plugin would take over cannot be read from the managed configuration
    /// at that point.
    /// </remarks>
    public int DeclaredSettingsCount { get; private set; }
    
    public async Task InitializeAsync(bool dryRun)
    {
        if(!this.TryProcessConfiguration(dryRun, out var issue))
            this.PluginIssues.Add(issue);

        if (!dryRun)
        {
            await PluginConfigurationObject.SyncManagedTokenizersAsync(this.Id, this.PluginPath);

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

    private static int ReadPriority(LuaState state)
    {
        if (state.Environment["PRIORITY"].TryRead<int>(out var priority))
            return priority;

        return 0;
    }

    /// <summary>
    /// Counts the settings a configuration plugin declares, ignoring the <c>.AllowUserOverride</c>
    /// companion keys: those refine a setting instead of adding one.
    /// </summary>
    private static int CountDeclaredSettings(LuaTable settingsTable)
    {
        const string USER_OVERRIDE_SUFFIX = ".AllowUserOverride";

        var count = 0;
        var previousKey = LuaValue.Nil;
        while (settingsTable.TryGetNext(previousKey, out var pair))
        {
            previousKey = pair.Key;
            if (pair.Key.TryRead<string>(out var settingName) && !settingName.EndsWith(USER_OVERRIDE_SUFFIX, StringComparison.Ordinal))
                count++;
        }

        return count;
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

        if (!TryValidateMinimumProviderConfidenceConfiguration(settingsTable, out message))
            return false;

        this.DeclaredSettingsCount = CountDeclaredSettings(settingsTable);
        
        // Config: check for updates, and if so, how often?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UpdateInterval, this.Id, settingsTable, dryRun);
        
        // Config: how should updates be installed?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UpdateInstallation, this.Id, settingsTable, dryRun);

        // Config: what should be the start page?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.StartPage, this.Id, settingsTable, dryRun);

        // Config: show prompt-injection alert dialogs?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShowPromptInjectionAlert, this.Id, settingsTable, dryRun);

        // Config: show built-in introduction on the home page?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShowIntroduction, this.Id, settingsTable, dryRun);

        // Config: show quick start guide on the home page?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShowQuickStartGuide, this.Id, settingsTable, dryRun);

        // Config: show last changelog on the home page?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShowLastChangelog, this.Id, settingsTable, dryRun);

        // Config: show vision panel on the home page?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ShowVision, this.Id, settingsTable, dryRun);

        // Config: allow the user to add providers?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.AllowUserToAddProvider, this.Id, settingsTable, dryRun);

        // Config: allow the user to import plugin archives?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.AllowUserToImportPlugins, this.Id, settingsTable, dryRun);

        // Config: allow the user to import configuration plugin archives?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.AllowUserToImportConfigurationPlugins, this.Id, settingsTable, dryRun);

        // Config: allow the user to share or export plugins?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.AllowUserToSharePlugins, this.Id, settingsTable, dryRun);

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

        // Config: global tool availability
        ManagedConfiguration.TryProcessConfiguration(x => x.Tools, x => x.EnableTools, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Tools, x => x.DisabledToolIds, this.Id, settingsTable, dryRun);

        // Config: minimum provider confidence per tool
        ManagedConfiguration.TryProcessConfiguration(x => x.Tools, x => x.MinimumProviderConfidenceByToolId, this.Id, settingsTable, dryRun);

        //
        // Config: settings of the individual tools, keyed by tool and field. Two tables rather
        // than a property per setting, so that tools an administrator's AI Studio does not know
        // at compile time — the ones plugin authors define — can be configured just the same.
        //
        ManagedConfiguration.TryProcessConfiguration(x => x.Tools, x => x.LockedToolSettings, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Tools, x => x.DefaultToolSettings, this.Id, settingsTable, dryRun);

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

        // Config: data source selection agent settings
        ManagedConfiguration.TryProcessConfiguration(x => x.AgentDataSourceSelection, x => x.PreselectAgentOptions, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.AgentDataSourceSelection, x => x.PreselectedAgentProvider, Guid.Empty, this.Id, settingsTable, dryRun);

        // Config: retrieval context validation agent settings
        ManagedConfiguration.TryProcessConfiguration(x => x.AgentRetrievalContextValidation, x => x.EnableRetrievalContextValidation, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.AgentRetrievalContextValidation, x => x.PreselectAgentOptions, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.AgentRetrievalContextValidation, x => x.PreselectedAgentProvider, Guid.Empty, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.AgentRetrievalContextValidation, x => x.NumParallelValidations, this.Id, settingsTable, dryRun);

        // Config: assistant plugin audit settings
        ManagedConfiguration.TryProcessConfiguration(x => x.AssistantPluginAudit, x => x.RequireAuditBeforeActivation, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.AssistantPluginAudit, x => x.PreselectedAgentProvider, Guid.Empty, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.AssistantPluginAudit, x => x.MinimumLevel, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.AssistantPluginAudit, x => x.BlockActivationBelowMinimum, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.AssistantPluginAudit, x => x.AutomaticallyAuditAssistants, this.Id, settingsTable, dryRun);

        // Config: enterprise-managed approvals for assistant plugins
        this.TryProcessEnterpriseApprovedAssistantPlugins(settingsTable, dryRun);
        
        // Handle configured LLM providers:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.LLM_PROVIDER, x => x.Providers, x => x.NextProviderNum, mainTable, this.Id, ref this.configObjects, dryRun, this.PluginPath);

        // Handle configured transcription providers:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER, x => x.TranscriptionProviders, x => x.NextTranscriptionNum, mainTable, this.Id, ref this.configObjects, dryRun, this.PluginPath);

        // Handle configured embedding providers:
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.EMBEDDING_PROVIDER, x => x.EmbeddingProviders, x => x.NextEmbeddingNum, mainTable, this.Id, ref this.configObjects, dryRun, this.PluginPath);

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
        ManagedConfiguration.TryProcessConfiguration(x => x.Chat, x => x.PreselectedDataSourcesDisabled, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Chat, x => x.PreselectedDataSourcesAutomaticSelection, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Chat, x => x.PreselectedDataSourcesAutomaticValidation, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Chat, x => x.PreselectedDataSourceIds, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.Chat, x => x.SendToChatDataSourceBehavior, this.Id, settingsTable, dryRun);

        // Config: Batch Processing Assistant defaults?
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.PreselectOptions, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.InputDirectory, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.OutputDirectory, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.FilePatterns, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.IncludeSubdirectories, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.PromptSource, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.FreePrompt, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.PromptFilePath, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.PreselectedPolicyId, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.OutputMode, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.ResultFileFormat, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.CsvFileName, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.ResultColumnHeader, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.CsvSeparator, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.CustomCsvSeparator, this.Id, settingsTable, dryRun);
        
        var minimumDelayIsValid = ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.MinimumDelaySeconds, this.Id, settingsTable, dryRun, validator: value => value is >= DataBatchProcessing.MIN_DELAY_SECONDS and <= DataBatchProcessing.MAX_DELAY_SECONDS);
        if (!minimumDelayIsValid && settingsTable.TryGetValue("DataBatchProcessing.MinimumDelaySeconds", out _))
            LOG.LogWarning("The Batch Processing minimum delay configured by plugin {ConfigPluginId} must be between {MinimumDelaySeconds} and {MaximumDelaySeconds} seconds.", this.Id, DataBatchProcessing.MIN_DELAY_SECONDS, DataBatchProcessing.MAX_DELAY_SECONDS);

        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.MinimumProviderConfidence, this.Id, settingsTable, dryRun);
        ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.PreselectedProvider, Guid.Empty, this.Id, settingsTable, dryRun);

        // Config: transcription provider?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UseTranscriptionProvider, Guid.Empty, this.Id, settingsTable, dryRun);

        message = string.Empty;
        return true;
    }

    private static bool TryValidateMinimumProviderConfidenceConfiguration(LuaTable settingsTable, out string message)
    {
        const string SETTING_NAME = "DataTools.MinimumProviderConfidenceByToolId";
        message = string.Empty;
        if (!settingsTable.TryGetValue(SETTING_NAME, out var configuredValue))
            return true;

        if (configuredValue.Type is not LuaValueType.Table || !configuredValue.TryRead<LuaTable>(out var configuredTable))
        {
            message = $"The setting '{SETTING_NAME}' must be a table of tool IDs and confidence levels.";
            return false;
        }

        var previousKey = LuaValue.Nil;
        while (configuredTable.TryGetNext(previousKey, out var pair))
        {
            previousKey = pair.Key;
            if (!pair.Key.TryRead<string>(out var toolId) || string.IsNullOrWhiteSpace(toolId) ||
                !pair.Value.TryRead<string>(out var configuredLevel) ||
                !Enum.TryParse<ConfidenceLevel>(configuredLevel, true, out var confidenceLevel) ||
                !Enum.IsDefined(confidenceLevel) ||
                confidenceLevel is ConfidenceLevel.UNKNOWN)
            {
                message = $"The setting '{SETTING_NAME}' contains an invalid tool ID or confidence level. Allowed confidence levels are NONE, UNTRUSTED, VERY_LOW, LOW, MODERATE, MEDIUM, and HIGH.";
                return false;
            }
        }

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

            // A configuration may list the same hash more than once, e.g. once to describe the
            // plugin and once to activate it. Combine those before anything else sees them:
            configuredApprovals = CombineApprovals(approvals);
            successful = true;
        }

        if (dryRun)
            return;

        //
        // Only a configuration which speaks for an organization may approve assistant plugins: one
        // deployed by a configuration server, or one staged in the test directory. An approval marks
        // a plugin as safe without any security audit, and the user interface states that the
        // organization approved it. No local configuration plugin may make that claim: it would
        // disable the security audit for arbitrary assistant plugins while telling the user that
        // their organization vouched for them.
        //
        // We decide by the plugin path. The self-declared DEPLOYED_USING_CONFIG_SERVER field would
        // not do, because any plugin can set it to true.
        //
        if (!PluginFactory.IsOrganizationConfigurationPath(this.PluginPath))
        {
            if (successful)
                LOG.LogWarning("The configuration plugin '{ConfigPluginId}' at '{PluginPath}' declares enterprise approvals for assistant plugins, but your organization's IT did not deploy it. Ignoring these approvals: only configuration plugins from a configuration server or from the test directory may approve assistant plugins.", this.Id, this.PluginPath);

            return;
        }

        if (PluginFactory.IsEnterpriseTestConfigurationPath(this.PluginPath))
            LOG.LogWarning("The test configuration plugin '{ConfigPluginId}' at '{PluginPath}' approves assistant plugins. These approvals are valid for this session only: AI Studio empties the test directory on every start.", this.Id, this.PluginPath);

        switch (successful)
        {
            case true:
                //
                // Approvals of several configuration plugins add up. An approval list is a pure
                // allowlist over hashes: not listing a plugin already means "not approved", so
                // replacing the list would only ever withdraw the approvals of another
                // configuration without expressing anything new.
                //
                configMeta.SetPluginContribution(configuredApprovals, this.Id);

                // Merge into the stored list right away, so the approvals of this plugin take
                // effect immediately. PluginFactory.LoadAll recomputes the authoritative list once
                // every configuration plugin has contributed:
                configMeta.SetValue(CombineApprovals(configMeta.GetValue().Concat(configuredApprovals)));
                configMeta.LockConfiguration(this.Id);
                break;

            case false when configMeta.IsLocked && configMeta.LockedByConfigPluginId == this.Id:
                configMeta.RemovePluginContribution(this.Id);
                configMeta.ResetLockedConfiguration();
                break;

            case false:
                configMeta.RemovePluginContribution(this.Id);
                break;
        }
    }

    /// <summary>
    /// Recomputes the effective enterprise approvals from the contributions of all configuration plugins.
    /// </summary>
    /// <remarks>
    /// Every configuration plugin merges its own approvals into the stored list while it starts, but
    /// nothing there can withdraw the approvals of a plugin which was removed in the meantime. This
    /// method rebuilds the list from the remaining contributions and is therefore called once all
    /// configuration plugins have been started.
    /// </remarks>
    /// <returns>True when the effective approvals changed, otherwise false.</returns>
    public static bool RefreshEnterpriseApprovedAssistantPlugins()
    {
        if (!ManagedConfiguration.TryGet(x => x.AssistantPluginAudit, x => x.EnterpriseApprovedPlugins, out ConfigMeta<DataAssistantPluginAudit, IList<DataAssistantPluginEnterpriseApproval>> configMeta))
            return false;

        var effectiveApprovals = CombineApprovals(configMeta.PluginContributions.Values.SelectMany(contribution => contribution));

        // Compare by what an approval decides, so a different order alone does not rewrite the
        // settings on every start, while a changed activation does reach the user:
        var currentApprovals = configMeta.GetValue();
        if (HaveApprovalsSameEffect(currentApprovals, effectiveApprovals))
            return false;

        LOG.LogInformation($"The enterprise approvals for assistant plugins changed from {currentApprovals.Count} to {effectiveApprovals.Count} entries, contributed by {configMeta.PluginContributions.Count} configuration plugin(s).");
        configMeta.SetValue(effectiveApprovals);
        return true;
    }

    /// <summary>
    /// Reduces approvals of several configuration plugins to one entry per assistant plugin hash.
    /// </summary>
    /// <remarks>
    /// Approving the same plugin twice is normal: a base configuration approves it for the whole
    /// organization, and a department configuration lists it again to activate it. Keeping only the
    /// entry seen first would silently drop what the other one asked for, and the contributions
    /// carry no guaranteed order, so which one that is could differ from start to start.
    /// </remarks>
    /// <param name="approvals">The approvals of all configuration plugins, in any order.</param>
    /// <returns>One approval per hash, in the order the hashes were first seen.</returns>
    private static List<DataAssistantPluginEnterpriseApproval> CombineApprovals(IEnumerable<DataAssistantPluginEnterpriseApproval> approvals)
    {
        var combined = new List<DataAssistantPluginEnterpriseApproval>();
        var positionByHash = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var approval in approvals)
        {
            if (positionByHash.TryGetValue(approval.PluginHash, out var position))
            {
                combined[position] = MergeApprovals(combined[position], approval);
                continue;
            }

            positionByHash[approval.PluginHash] = combined.Count;
            combined.Add(approval);
        }

        return combined;
    }

    /// <summary>
    /// Combines two approvals of the same assistant plugin hash into a single one.
    /// </summary>
    /// <remarks>
    /// The two activation fields are combined in opposite directions on purpose. One configuration
    /// asking for the activation is enough to activate, because not asking for it says nothing
    /// against it. The freedom to switch the assistant off again, however, only survives when every
    /// configuration which does ask for the activation grants it: otherwise a department could take
    /// back a lock the organization deliberately set. An approval which does not ask for the
    /// activation at all expresses nothing about that freedom and is therefore not counted.<br/><br/>
    /// The result of these two fields does not depend on the order the approvals arrive in. For the
    /// descriptive fields, the first value which says anything wins, and the approval date is the
    /// earliest one given: the plugin has been approved since then.
    /// </remarks>
    /// <param name="first">The approval seen first.</param>
    /// <param name="second">The approval to combine it with.</param>
    /// <returns>The combined approval.</returns>
    private static DataAssistantPluginEnterpriseApproval MergeApprovals(DataAssistantPluginEnterpriseApproval first, DataAssistantPluginEnterpriseApproval second) => new()
    {
        PluginHash = first.PluginHash,
        DisplayName = string.IsNullOrWhiteSpace(first.DisplayName) ? second.DisplayName : first.DisplayName,
        Comment = string.IsNullOrWhiteSpace(first.Comment) ? second.Comment : first.Comment,
        ApprovedBy = string.IsNullOrWhiteSpace(first.ApprovedBy) ? second.ApprovedBy : first.ApprovedBy,
        ApprovedAtUtc = EarliestApprovalTime(first.ApprovedAtUtc, second.ApprovedAtUtc),

        Activate = first.Activate || second.Activate,
        AllowUserOverride = (first.Activate, second.Activate) switch
        {
            (true, true) => first.AllowUserOverride && second.AllowUserOverride,
            (true, false) => first.AllowUserOverride,
            (false, true) => second.AllowUserOverride,
            _ => false,
        },
    };

    private static DateTimeOffset? EarliestApprovalTime(DateTimeOffset? first, DateTimeOffset? second) => (first, second) switch
    {
        (null, _) => second,
        (_, null) => first,
        _ => first <= second ? first : second,
    };

    /// <summary>
    /// Checks whether two approval lists decide the same thing for every assistant plugin.
    /// </summary>
    /// <remarks>
    /// This is what tells a rewrite of the settings apart from a mere reordering of the same
    /// approvals. Only the hash and the two activation fields are compared: the descriptive fields
    /// change nothing about what an approval does, and rewriting the settings because a comment was
    /// reworded would store the file on every start.
    /// </remarks>
    /// <param name="currentApprovals">The approvals currently stored in the settings.</param>
    /// <param name="effectiveApprovals">The approvals recomputed from the contributions.</param>
    /// <returns>True when both lists have the same effect, otherwise false.</returns>
    private static bool HaveApprovalsSameEffect(IList<DataAssistantPluginEnterpriseApproval> currentApprovals, IList<DataAssistantPluginEnterpriseApproval> effectiveApprovals)
    {
        if (currentApprovals.Count != effectiveApprovals.Count)
            return false;

        var currentByHash = new Dictionary<string, DataAssistantPluginEnterpriseApproval>(StringComparer.Ordinal);
        foreach (var approval in currentApprovals)
            currentByHash[approval.PluginHash] = approval;

        foreach (var effectiveApproval in effectiveApprovals)
        {
            if (!currentByHash.TryGetValue(effectiveApproval.PluginHash, out var currentApproval))
                return false;

            if (currentApproval.Activate != effectiveApproval.Activate || currentApproval.AllowUserOverride != effectiveApproval.AllowUserOverride)
                return false;
        }

        return true;
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
        var activate = TryReadOptionalBool(table, "Activate", index, configPluginId);
        var allowUserOverride = TryReadOptionalBool(table, "AllowUserOverride", index, configPluginId);

        if (allowUserOverride && !activate)
            LOG.LogWarning("The enterprise assistant approval entry at index {Index} allows the user to override an activation it never asks for. 'AllowUserOverride' has no effect without 'Activate' (config plugin id: {ConfigPluginId}).", index, configPluginId);

        approval = new()
        {
            PluginHash = normalizedHash,
            DisplayName = displayName,
            Comment = comment,
            ApprovedBy = approvedBy,
            ApprovedAtUtc = approvedAtUtc,
            Activate = activate,
            AllowUserOverride = allowUserOverride,
        };
        return true;
    }

    private static string TryReadOptionalString(LuaTable table, string key)
    {
        return table.TryGetValue(key, out var value) && value.TryRead<string>(out var text)
            ? text
            : string.Empty;
    }

    private static bool TryReadOptionalBool(LuaTable table, string key, int index, Guid configPluginId)
    {
        if (!table.TryGetValue(key, out var value))
            return false;

        if (value.TryRead<bool>(out var flag))
            return flag;

        LOG.LogWarning("The enterprise assistant approval entry at index {Index} contains an invalid {Key} value. Expected a boolean (config plugin id: {ConfigPluginId}).", index, key, configPluginId);
        return false;
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
