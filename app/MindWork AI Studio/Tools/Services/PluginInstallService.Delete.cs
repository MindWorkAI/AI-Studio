using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Media;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools.Services;

public sealed partial class PluginInstallService
{
    /// <summary>
    /// The plugin types users may remove through the user interface.
    /// </summary>
    private static readonly PluginType[] DELETABLE_PLUGIN_TYPES = [PluginType.ASSISTANT, PluginType.CONFIGURATION, PluginType.LANGUAGE];

    /// <summary>
    /// Checks whether a plugin is one that users may delete.
    /// </summary>
    /// <remarks>
    /// This decides whether the delete action is offered at all. Whether it may run right now is a
    /// different question: an assistant with running background work stays visible but blocked.
    /// </remarks>
    public static bool CanDeletePlugin(IAvailablePlugin plugin) => string.IsNullOrWhiteSpace(GetDeletionEligibilityIssue(plugin));

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
    /// Deletes the directory of a plugin the user installed or placed themselves.
    /// The directory gets moved to a backup dir outside the plugin root so the plugin loader cannot
    /// discover it during reload. On failure, the directory and the related settings are restored.
    /// </summary>
    /// <remarks>
    /// For a configuration plugin, we do not remove its providers, data sources, chat templates,
    /// profiles, or locked settings ourselves. The reload does that: it recognizes them as left over
    /// once their configuration plugin is gone, and it also deletes the related secrets from the OS
    /// keyring.<br/><br/>
    /// What the reload cannot recognize as left over is everything the user decided about the plugin
    /// itself: its activation state, the language choice of a language plugin, and the security audit
    /// of an assistant. Those are removed here, see ApplyDeleteSideEffects.
    /// </remarks>
    /// <param name="plugin">Metadata of the plugin to delete.</param>
    /// <param name="token">Cancellation token for settings storage and plugin reload.</param>
    /// <returns>
    /// Delete result that contains a success state, deleted plugin metadata, the original plugin directory,
    /// and a user-facing issue when deletion failed.
    /// </returns>
    public async Task<PluginDeleteResult> DeletePluginAsync(IAvailablePlugin plugin, CancellationToken token)
    {
        var deletionIssue = this.GetDeletionIssue(plugin);
        if (!string.IsNullOrWhiteSpace(deletionIssue))
            return DeleteError(plugin, plugin.LocalPath, deletionIssue);

        await this.installSemaphore.WaitAsync(token);
        var pluginDirectory = plugin.LocalPath;
        var backupDirectory = string.Empty;
        var sideEffects = PluginDeleteSideEffects.NONE;

        try
        {
            // Check again under the semaphore: another operation might have changed the plugin state
            // while we were waiting:
            deletionIssue = this.GetDeletionIssue(plugin);
            if (!string.IsNullOrWhiteSpace(deletionIssue))
                return DeleteError(plugin, pluginDirectory, deletionIssue);

            backupDirectory = CreateDeleteBackupDirectory(plugin);
            Directory.CreateDirectory(Path.GetDirectoryName(backupDirectory)!);
            this.MoveDirectory(pluginDirectory, backupDirectory);

            sideEffects = this.ApplyDeleteSideEffects(plugin);
            if (sideEffects.HasChanges)
                await this.settingsManager.StoreSettings();

            await PluginFactory.LoadAll(token);

            TryDeleteDirectory(backupDirectory, "plugin delete backup", this.logger);
            this.logger.LogInformation($"Deleted {plugin.Type} plugin '{plugin.Name}' ({plugin.Id}) from '{pluginDirectory}'.");
            return new(true, plugin.Id, plugin.Name, pluginDirectory, string.Empty);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, $"Failed to delete {plugin.Type} plugin '{plugin.Name}' ({plugin.Id}) from '{pluginDirectory}'.");

            await this.TryRestoreDeletedPluginAsync(plugin, pluginDirectory, backupDirectory, sideEffects, token);
            return DeleteError(plugin, pluginDirectory, string.Format(TB("Unexpected error: {0}"), e.Message));
        }
        finally
        {
            this.installSemaphore.Release();
        }
    }

    /// <summary>
    /// Checks everything that prevents deleting a plugin right now.
    /// </summary>
    private string GetDeletionIssue(IAvailablePlugin plugin)
    {
        var eligibilityIssue = GetDeletionEligibilityIssue(plugin);
        if (!string.IsNullOrWhiteSpace(eligibilityIssue))
            return eligibilityIssue;

        // An assistant must not be pulled away from under a user while it is still working:
        if (plugin.Type is PluginType.ASSISTANT && this.HasActiveAssistantWork(plugin.Id))
            return TB("The assistant cannot be deleted while background work is still running.");

        return string.Empty;
    }

    /// <summary>
    /// Checks whether a plugin is one users may delete at all, regardless of its current state.
    /// </summary>
    private static string GetDeletionEligibilityIssue(IAvailablePlugin plugin)
    {
        if (!DELETABLE_PLUGIN_TYPES.Contains(plugin.Type))
            return TB("Only assistant, configuration, and language plugins can be deleted.");

        if (plugin.IsInternal)
            return TB("Plugins shipped with AI Studio cannot be deleted.");

        if (string.IsNullOrWhiteSpace(plugin.LocalPath))
            return TB("The plugin has no local directory.");

        //
        // We decide by the plugin path, not by what a plugin declares about itself. Both
        // DEPLOYED_USING_CONFIG_SERVER and the Assistant Builder metadata are self-declared: a
        // locally placed plugin could claim to be deployed by an organization, or simply omit the
        // builder metadata, and would then be impossible to remove through the user interface, which
        // is exactly the situation this deletion is meant to resolve.
        //
        if (PluginFactory.IsEnterpriseConfigurationPath(plugin.LocalPath))
            return TB("Plugins deployed by your organization cannot be deleted.");

        if (!PluginFactory.IsInsidePluginsRoot(plugin.LocalPath) || PluginFactory.IsPluginsRoot(plugin.LocalPath))
            return TB("This individual plugin’s directory is outside the expected plugins directory.");

        return Directory.Exists(plugin.LocalPath) ? string.Empty : TB("The plugin directory does not exist.");
    }

    /// <summary>
    /// Removes everything the user decided about the plugin, and reports what was removed so a failed
    /// deletion can put it back.
    /// </summary>
    private PluginDeleteSideEffects ApplyDeleteSideEffects(IAvailablePlugin plugin)
    {
        var configurationData = this.settingsManager.ConfigurationData;

        //
        // Nothing removes the activation state of a plugin which is gone. Should the user install
        // a plugin with the same ID again later, it would start enabled without ever having been
        // switched on. We ask for removal regardless of the plugin type: a configuration plugin
        // is never listed there, so this simply does nothing for it:
        //
        var wasEnabled = configurationData.EnabledPlugins.Remove(plugin.Id);

        //
        // When the user had chosen this language plugin, the app would silently fall back to
        // English while the settings still point to the deleted plugin. We return the language
        // choice to automatic instead, so the settings stay truthful:
        //
        var wasChosenLanguage = plugin.Type is PluginType.LANGUAGE && configurationData.App.LanguagePluginId == plugin.Id;
        if (wasChosenLanguage)
        {
            configurationData.App.LanguageBehavior = LangBehavior.AUTO;
            configurationData.App.LanguagePluginId = Guid.Empty;
        }

        //
        // The security audit belongs to the assistant code we checked. Another assistant installed
        // under the same ID later is different code, so it must be audited again:
        //
        List<PluginAssistantAudit> removedAudits = [];
        if (plugin.Type is PluginType.ASSISTANT)
        {
            removedAudits = [.. configurationData.AssistantPluginAudits.Where(audit => audit.PluginId == plugin.Id)];
            if (removedAudits.Count > 0)
                configurationData.AssistantPluginAudits.RemoveAll(audit => audit.PluginId == plugin.Id);
        }

        return new(wasEnabled, wasChosenLanguage, removedAudits);
    }

    private static string CreateDeleteBackupDirectory(IAvailablePlugin plugin)
    {
        var backupRoot = Path.Join(SettingsManager.DataDirectory, DELETE_BACKUP_DIRECTORY);
        return Path.Join(backupRoot, $"{plugin.Type.GetDirectory()}-{plugin.Id:N}-{Guid.NewGuid():N}");
    }

    private async Task TryRestoreDeletedPluginAsync(IAvailablePlugin plugin, string pluginDirectory, string backupDirectory, PluginDeleteSideEffects sideEffects, CancellationToken token)
    {
        try
        {
            if (!Directory.Exists(pluginDirectory) && Directory.Exists(backupDirectory))
                this.MoveDirectory(backupDirectory, pluginDirectory);

            var configurationData = this.settingsManager.ConfigurationData;
            if (sideEffects.WasEnabled && !configurationData.EnabledPlugins.Contains(plugin.Id))
                configurationData.EnabledPlugins.Add(plugin.Id);

            if (sideEffects.WasChosenLanguage)
            {
                configurationData.App.LanguageBehavior = LangBehavior.MANUAL;
                configurationData.App.LanguagePluginId = plugin.Id;
            }

            if (sideEffects.RemovedAudits.Count > 0)
            {
                configurationData.AssistantPluginAudits.RemoveAll(audit => audit.PluginId == plugin.Id);
                configurationData.AssistantPluginAudits.AddRange(sideEffects.RemovedAudits);
            }

            if (sideEffects.HasChanges)
                await this.settingsManager.StoreSettings();

            // The reload restores everything the plugin configured, because it is back in place:
            await PluginFactory.LoadAll(token);
        }
        catch (Exception restoreException)
        {
            this.logger.LogError(restoreException, $"Failed to restore {plugin.Type} plugin '{plugin.Name}' ({plugin.Id}) after a failed delete.");
        }
    }

    /// <summary>
    /// What deleting a plugin changed in the settings, so a failed deletion can undo it.
    /// </summary>
    private sealed record PluginDeleteSideEffects(bool WasEnabled, bool WasChosenLanguage, List<PluginAssistantAudit> RemovedAudits)
    {
        public static readonly PluginDeleteSideEffects NONE = new(false, false, []);

        public bool HasChanges => this.WasEnabled || this.WasChosenLanguage || this.RemovedAudits.Count > 0;
    }
}