using AIStudio.Settings;
using AIStudio.Tools.Media;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools.Services;

public sealed partial class PluginInstallService
{
    /// <summary>
    /// Checks whether a local plugin is an Assistant Builder generated assistant that users may delete.
    /// </summary>
    public static bool CanDeleteInstalledAssistant(IAvailablePlugin plugin) => string.IsNullOrWhiteSpace(GetAssistantDeletionEligibilityIssue(plugin));

    /// <summary>
    /// Checks whether a plugin is a local configuration plugin that users may delete.
    /// </summary>
    public static bool CanDeleteInstalledConfiguration(IAvailablePlugin plugin) => string.IsNullOrWhiteSpace(GetConfigurationDeletionEligibilityIssue(plugin));

    /// <summary>
    /// Collects what deleting a local configuration plugin removes besides the plugin directory.
    /// </summary>
    /// <param name="plugin">The configuration plugin about to be deleted.</param>
    /// <returns>
    /// The summary shown to the user before the deletion starts. It is empty when the plugin is not
    /// running, because we cannot tell what an unloadable plugin had configured.
    /// </returns>
    public ConfigurationPluginDeleteSummary BuildConfigurationDeleteSummary(IAvailablePlugin plugin)
    {
        var configurationPlugin = PluginFactory.RunningPlugins.OfType<PluginConfiguration>().FirstOrDefault(candidate => candidate.Id == plugin.Id);
        if (configurationPlugin is null)
            return ConfigurationPluginDeleteSummary.EMPTY;

        var configObjects = configurationPlugin.ConfigObjects.ToList();
        var configurationData = this.settingsManager.ConfigurationData;

        // Both maps record which configuration plugin manages a setting. Everything this plugin owns
        // returns to its default value once the plugin is gone:
        var lockedSettings =
            configurationData.ManagedLockedConfigurations.Count(entry => entry.Value == plugin.Id) +
            configurationData.ManagedEditableDefaults.Count(entry => entry.Value.ConfigPluginId == plugin.Id);

        return new(
            LlmProviders: CountObjects(PluginConfigurationObjectType.LLM_PROVIDER),
            TranscriptionProviders: CountObjects(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER),
            EmbeddingProviders: CountObjects(PluginConfigurationObjectType.EMBEDDING_PROVIDER),
            DataSources: CountObjects(PluginConfigurationObjectType.DATA_SOURCE),
            ChatTemplates: CountObjects(PluginConfigurationObjectType.CHAT_TEMPLATE),
            Profiles: CountObjects(PluginConfigurationObjectType.PROFILE),
            DocumentAnalysisPolicies: CountObjects(PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY),
            LockedSettings: lockedSettings,
            MandatoryInfos: configurationPlugin.MandatoryInfos.Count,
            Introductions: configurationPlugin.Introductions.Count);

        int CountObjects(PluginConfigurationObjectType type) => configObjects.Count(configObject => configObject.Type == type);
    }

    /// <summary>
    /// Checks whether an assistant still owns running or canceling background work.
    /// </summary>
    public bool HasActiveAssistantWork(Guid pluginId)
    {
        var instanceId = pluginId.ToString();
        if (this.assistantSessionService.GetSnapshots().Any(snapshot => snapshot.IsActive && string.Equals(snapshot.Key.InstanceId, instanceId, StringComparison.Ordinal)))
            return true;

        var ownerIdSuffix = $":{instanceId}";
        return this.mediaTranscriptionService.GetSnapshots().Any(snapshot =>
            snapshot is { IsBusy: true, Owner.Kind: MediaImportOwnerKind.ASSISTANT } &&
            snapshot.Owner.Id.EndsWith(ownerIdSuffix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Deletes installed local assistant plugin directories.
    /// The directory gets moved to a backup dir outside the plugin root so the
    /// plugin loader cannot discover it during reload. On failure, the directory
    /// and related assistant settings are restored.
    /// </summary>
    /// <param name="plugin">Assistant plugin metadata</param>
    /// <param name="token">Cancellation token for settings storage and plugin reload</param>
    /// <returns>
    /// Delete result that contains success state, deleted plugin metadata, the original plugin directory,
    /// and a user-facing issue when deletion failed.
    /// </returns>
    public async Task<PluginDeleteResult> DeleteInstalledAssistantAsync(IAvailablePlugin plugin, CancellationToken token)
    {
        var eligibilityIssue = GetAssistantDeletionEligibilityIssue(plugin);
        if (!string.IsNullOrEmpty(eligibilityIssue))
            return DeleteError(plugin, plugin.LocalPath, eligibilityIssue);

        if (this.HasActiveAssistantWork(plugin.Id))
            return DeleteError(plugin, plugin.LocalPath, TB("The assistant cannot be deleted while background work is still running."));

        await this.installSemaphore.WaitAsync(token);
        var pluginDirectory = plugin.LocalPath;
        var backupDirectory = string.Empty;
        var wasEnabled = false;
        var removedAudits = new List<PluginAssistantAudit>();

        try
        {
            eligibilityIssue = GetAssistantDeletionEligibilityIssue(plugin);
            if (!string.IsNullOrEmpty(eligibilityIssue))
                return DeleteError(plugin, pluginDirectory, eligibilityIssue);

            if (this.HasActiveAssistantWork(plugin.Id))
                return DeleteError(plugin, pluginDirectory, TB("The assistant cannot be deleted while background work is still running."));

            backupDirectory = CreateDeleteBackupDirectory(plugin, "assistant");
            Directory.CreateDirectory(Path.GetDirectoryName(backupDirectory)!);
            Directory.Move(pluginDirectory, backupDirectory);

            wasEnabled = this.settingsManager.ConfigurationData.EnabledPlugins.Remove(plugin.Id);
            removedAudits =
            [
                .. this.settingsManager.ConfigurationData.AssistantPluginAudits.Where(audit => audit.PluginId == plugin.Id)
            ];

            if (removedAudits.Count > 0)
                this.settingsManager.ConfigurationData.AssistantPluginAudits.RemoveAll(audit => audit.PluginId == plugin.Id);

            await this.settingsManager.StoreSettings();
            await PluginFactory.LoadAll(token);

            TryDeleteDirectory(backupDirectory, "assistant plugin delete backup", this.logger);
            this.logger.LogInformation($"Deleted assistant plugin '{plugin.Name}' ({plugin.Id}) from '{pluginDirectory}'.");
            return new(true, plugin.Id, plugin.Name, pluginDirectory, string.Empty);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, $"Failed to delete assistant plugin '{plugin.Name}' ({plugin.Id}) from '{pluginDirectory}'.");

            await this.TryRestoreDeletedAssistantPluginAsync(plugin, pluginDirectory, backupDirectory, wasEnabled, removedAudits, token);
            return DeleteError(plugin, pluginDirectory, string.Format(TB("Unexpected error: {0}"), e.Message));
        }
        finally
        {
            this.installSemaphore.Release();
        }
    }

    /// <summary>
    /// Deletes a local configuration plugin directory.
    /// The directory gets moved to a backup dir outside the plugin root so the plugin loader cannot
    /// discover it during reload. On failure, the directory is restored.
    /// </summary>
    /// <remarks>
    /// We do not remove the providers, data sources, chat templates, profiles, or locked settings of
    /// the plugin ourselves. The reload does that: it recognizes them as left over once their
    /// configuration plugin is gone, and it also deletes the related secrets from the OS keyring.
    /// </remarks>
    /// <param name="plugin">Configuration plugin metadata.</param>
    /// <param name="token">Cancellation token for the plugin reload.</param>
    /// <returns>
    /// Delete result that contains success state, deleted plugin metadata, the original plugin directory,
    /// and a user-facing issue when deletion failed.
    /// </returns>
    public async Task<PluginDeleteResult> DeleteInstalledConfigurationAsync(IAvailablePlugin plugin, CancellationToken token)
    {
        var eligibilityIssue = GetConfigurationDeletionEligibilityIssue(plugin);
        if (!string.IsNullOrEmpty(eligibilityIssue))
            return DeleteError(plugin, plugin.LocalPath, eligibilityIssue);

        await this.installSemaphore.WaitAsync(token);
        var pluginDirectory = plugin.LocalPath;
        var backupDirectory = string.Empty;

        try
        {
            // Check again under the semaphore: another operation might have changed the plugin state
            // while we were waiting:
            eligibilityIssue = GetConfigurationDeletionEligibilityIssue(plugin);
            if (!string.IsNullOrEmpty(eligibilityIssue))
                return DeleteError(plugin, pluginDirectory, eligibilityIssue);

            backupDirectory = CreateDeleteBackupDirectory(plugin, "configuration");
            Directory.CreateDirectory(Path.GetDirectoryName(backupDirectory)!);
            Directory.Move(pluginDirectory, backupDirectory);

            await PluginFactory.LoadAll(token);

            TryDeleteDirectory(backupDirectory, "configuration plugin delete backup", this.logger);
            this.logger.LogInformation($"Deleted configuration plugin '{plugin.Name}' ({plugin.Id}) from '{pluginDirectory}'.");
            return new(true, plugin.Id, plugin.Name, pluginDirectory, string.Empty);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, $"Failed to delete configuration plugin '{plugin.Name}' ({plugin.Id}) from '{pluginDirectory}'.");

            await this.TryRestoreDeletedConfigurationPluginAsync(plugin, pluginDirectory, backupDirectory, token);
            return DeleteError(plugin, pluginDirectory, string.Format(TB("Unexpected error: {0}"), e.Message));
        }
        finally
        {
            this.installSemaphore.Release();
        }
    }

    private static string GetAssistantDeletionEligibilityIssue(IAvailablePlugin plugin)
    {
        if (plugin.Type is not PluginType.ASSISTANT)
            return TB("Only assistant plugins can be deleted.");

        if (plugin.IsInternal)
            return TB("Internal assistant plugins cannot be deleted.");

        if (plugin.IsManagedByConfigServer)
            return TB("Config Server managed assistant plugins cannot be deleted.");

        if (string.IsNullOrWhiteSpace(plugin.LocalPath))
            return TB("The assistant plugin has no local directory.");

        var assistantPlugin = PluginFactory.RunningPlugins
            .OfType<PluginAssistants>()
            .FirstOrDefault(candidate => candidate.Id == plugin.Id && IsSameDirectory(candidate.PluginPath, plugin.LocalPath));

        if (assistantPlugin is null || assistantPlugin.IsInternal || !assistantPlugin.IsAssistantBuilderGenerated)
            return TB("Only assistants generated by the Assistant Builder can be deleted.");

        if (assistantPlugin.IsManagedByConfigServer)
            return TB("Config Server managed assistant plugins cannot be deleted.");

        if (!TryGetPluginRoot(PluginType.ASSISTANT, out var assistantPluginsRoot, out var rootIssue))
            return rootIssue;

        if (!IsPathInsideDirectory(assistantPluginsRoot, plugin.LocalPath) || IsSameDirectory(assistantPluginsRoot, plugin.LocalPath))
            return TB("The assistant plugin directory is outside the local assistant plugin directory.");

        return Directory.Exists(plugin.LocalPath)
            ? string.Empty
            : TB("The assistant plugin directory does not exist.");
    }

    private static string GetConfigurationDeletionEligibilityIssue(IAvailablePlugin plugin)
    {
        if (plugin.Type is not PluginType.CONFIGURATION)
            return TB("Only configuration plugins can be deleted this way.");

        if (plugin.IsInternal)
            return TB("Internal configuration plugins cannot be deleted.");

        if (string.IsNullOrWhiteSpace(plugin.LocalPath))
            return TB("The configuration plugin has no local directory.");

        //
        // We decide by the plugin path, not by IsManagedByConfigServer. That value comes from the
        // plugin's own DEPLOYED_USING_CONFIG_SERVER field: a locally placed plugin could declare
        // itself managed and would then be impossible to remove through the user interface, which
        // is exactly the situation this deletion is meant to resolve.
        //
        if (PluginFactory.IsEnterpriseConfigurationPath(plugin.LocalPath))
            return TB("Configuration plugins deployed by your organization cannot be deleted.");

        if (!PluginFactory.IsInsidePluginsRoot(plugin.LocalPath))
            return TB("The configuration plugin directory is outside the plugins directory.");

        return Directory.Exists(plugin.LocalPath)
            ? string.Empty
            : TB("The configuration plugin directory does not exist.");
    }

    private static string CreateDeleteBackupDirectory(IAvailablePlugin plugin, string pluginKind)
    {
        var backupRoot = Path.Join(SettingsManager.DataDirectory, DELETE_BACKUP_DIRECTORY);
        return Path.Join(backupRoot, $"{pluginKind}-{plugin.Id:N}-{Guid.NewGuid():N}");
    }

    private async Task TryRestoreDeletedAssistantPluginAsync(IAvailablePlugin plugin, string pluginDirectory, string backupDirectory, bool wasEnabled, List<PluginAssistantAudit> removedAudits, CancellationToken token)
    {
        try
        {
            if (!Directory.Exists(pluginDirectory) && Directory.Exists(backupDirectory))
                Directory.Move(backupDirectory, pluginDirectory);

            if (wasEnabled && !this.settingsManager.ConfigurationData.EnabledPlugins.Contains(plugin.Id))
                this.settingsManager.ConfigurationData.EnabledPlugins.Add(plugin.Id);

            if (removedAudits.Count > 0)
            {
                this.settingsManager.ConfigurationData.AssistantPluginAudits.RemoveAll(audit => audit.PluginId == plugin.Id);
                this.settingsManager.ConfigurationData.AssistantPluginAudits.AddRange(removedAudits);
            }

            await this.settingsManager.StoreSettings();
            await PluginFactory.LoadAll(token);
        }
        catch (Exception restoreException)
        {
            this.logger.LogError(restoreException, $"Failed to restore assistant plugin '{plugin.Name}' ({plugin.Id}) after a failed delete.");
        }
    }

    private async Task TryRestoreDeletedConfigurationPluginAsync(IAvailablePlugin plugin, string pluginDirectory, string backupDirectory, CancellationToken token)
    {
        try
        {
            if (!Directory.Exists(pluginDirectory) && Directory.Exists(backupDirectory))
                Directory.Move(backupDirectory, pluginDirectory);

            // The reload restores everything the plugin configured, because it is back in place:
            await PluginFactory.LoadAll(token);
        }
        catch (Exception restoreException)
        {
            this.logger.LogError(restoreException, $"Failed to restore configuration plugin '{plugin.Name}' ({plugin.Id}) after a failed delete.");
        }
    }
}