using System.Text;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools.Services;

public sealed partial class PluginInstallService
{
    private async Task<AssistantPluginInstallResult> InstallStagedPluginAsync(string pluginRoot, PluginValidationResult validation, PluginType pluginType, CancellationToken token)
    {
        var stagingDirectory = validation.StagingDirectory;
        var plugin = validation.Plugin!;
        string? backupDirectory = null;
        string? finalDirectory = null;
        var replacedExisting = false;
        var movedIntoPlace = false;

        try
        {
            Directory.CreateDirectory(pluginRoot);
            finalDirectory = DetermineFinalDirectory(pluginRoot, plugin, pluginType);
            if (!IsPathInsideDirectory(pluginRoot, finalDirectory))
                return Error(TB("The resolved plugin directory is outside the plugin directory."));

            var replacementIssue = GetReplacementIssue(plugin.Id, pluginType);
            if (!string.IsNullOrWhiteSpace(replacementIssue))
                return Error(replacementIssue);

            if (Directory.Exists(finalDirectory))
            {
                replacedExisting = true;

                // The backup goes to a directory outside the plugin root, so the plugin loader
                // cannot discover it during the reload below. Otherwise, the previous version
                // would be loaded a second time, next to the version we are installing:
                backupDirectory = CreateInstallBackupDirectory(plugin);
                Directory.CreateDirectory(Path.GetDirectoryName(backupDirectory)!);
                Directory.Move(finalDirectory, backupDirectory);
            }

            Directory.Move(stagingDirectory, finalDirectory);
            movedIntoPlace = true;
            await PluginFactory.LoadAll(token);

            if (!string.IsNullOrWhiteSpace(backupDirectory))
                TryDeleteDirectory(backupDirectory, "plugin backup", this.logger);

            this.logger.LogInformation("Installed plugin '{PluginName}' ({PluginId}, {PluginType}) to '{PluginDirectory}'.", plugin.Name, plugin.Id, pluginType, finalDirectory);
            return new(true, plugin.Id, plugin.Name, finalDirectory, replacedExisting, string.Empty);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to install plugin.");

            // Only remove the target directory when this installation actually moved the plugin
            // there. Otherwise, when moving the previous plugin into the backup directory failed,
            // we would delete the still intact previous plugin:
            if (movedIntoPlace && !string.IsNullOrWhiteSpace(finalDirectory) && Directory.Exists(finalDirectory))
                TryDeleteDirectory(finalDirectory, "failed assistant plugin installation", this.logger);

            if (!string.IsNullOrWhiteSpace(backupDirectory) && Directory.Exists(backupDirectory) && !string.IsNullOrWhiteSpace(finalDirectory) && !Directory.Exists(finalDirectory))
            {
                try
                {
                    Directory.Move(backupDirectory, finalDirectory);
                    await PluginFactory.LoadAll(CancellationToken.None);
                }
                catch (Exception restoreException)
                {
                    this.logger.LogError(restoreException, "Failed to restore the previous assistant plugin after a failed installation.");
                }
            }

            return Error(string.Format(TB("Unexpected error: {0}"), e.Message));
        }
        finally
        {
            this.TryDeleteStagingDirectory(stagingDirectory);
        }
    }

    /// <summary>
    /// Loads and validates plugin code that is not installed yet.
    /// </summary>
    /// <param name="pluginDirectory">The staging directory the plugin currently lives in.</param>
    /// <param name="pluginCode">The <c>plugin.lua</c> content to validate.</param>
    /// <param name="acceptedTypes">The plugin types the caller accepts.</param>
    /// <param name="wrongTypeIssue">Issue when the plugin has another type. Gets the plugin issues as {0}.</param>
    /// <param name="invalidPluginIssue">Issue when the plugin is of an accepted type, but invalid. Gets the plugin issues as {0}.</param>
    /// <param name="conflictingPluginIdIssue">Issue when another plugin already uses this plugin ID.</param>
    /// <param name="token">Cancellation token for running the Lua code.</param>
    /// <returns>The validation result, including the loaded plugin when it passed.</returns>
    private static async Task<PluginValidationResult> ValidatePluginCodeAsync(string pluginDirectory, string pluginCode, IReadOnlyCollection<PluginType> acceptedTypes,
        string wrongTypeIssue, string invalidPluginIssue, string conflictingPluginIdIssue, CancellationToken token)
    {
        // The plugin is not installed yet: it sits in a staging directory outside the installed
        // plugins directory. We allow that directory as the module base, so the plugin can load its
        // own Lua modules, e.g., an icon.lua, while we validate it:
        var plugin = await PluginFactory.Load(pluginDirectory, pluginCode, token, pluginDirectory);
        if (!acceptedTypes.Contains(plugin.Type))
            return PluginValidationResult.Failure(string.Format(wrongTypeIssue, string.Join("; ", plugin.Issues)));

        if (!plugin.IsValid)
            return PluginValidationResult.Failure(string.Format(invalidPluginIssue, string.Join("; ", plugin.Issues)));

        // Plugin IDs must be unique across all plugin types: several lookups resolve a plugin by its
        // ID alone, e.g., the base language plugin in PluginFactory.Starting. A plugin carrying the
        // ID of a plugin of another type would break those lookups. Reusing the ID of another local
        // plugin of the same type stays allowed: that is how updating one works.
        if (PluginFactory.AvailablePlugins.Any(availablePlugin => availablePlugin.Id == plugin.Id && (availablePlugin.IsInternal || availablePlugin.Type != plugin.Type)))
            return PluginValidationResult.Failure(conflictingPluginIdIssue);

        return new(true, string.Empty, plugin, string.Empty);
    }

    /// <summary>
    /// Determines the directory local plugins of the given type are installed into.
    /// </summary>
    private static bool TryGetPluginRoot(PluginType pluginType, out string pluginRoot, out string issue)
    {
        pluginRoot = string.Empty;
        issue = string.Empty;

        var dataDirectory = SettingsManager.DataDirectory;
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            issue = TB("The AI Studio data directory is not initialized yet.");
            return false;
        }

        pluginRoot = Path.Join(dataDirectory, "plugins", pluginType.GetDirectory());
        return true;
    }

    private static string DetermineFinalDirectory(string pluginRoot, IPluginMetadata plugin, PluginType pluginType)
    {
        var existingPlugin = FindReplaceablePlugin(plugin.Id, pluginType);
        return existingPlugin is not null
            ? existingPlugin.LocalPath
            : Path.Join(pluginRoot, CreatePluginDirectoryName(plugin));
    }

    /// <summary>
    /// Finds the local plugin that an installation with the given ID and type would replace.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin about to be installed.</param>
    /// <param name="pluginType">The type of the plugin about to be installed.</param>
    /// <returns>The plugin that would be replaced, or null when the installation adds a new plugin.</returns>
    private static IAvailablePlugin? FindReplaceablePlugin(Guid pluginId, PluginType pluginType) => PluginFactory.AvailablePlugins
        .OfType<IAvailablePlugin>()
        .FirstOrDefault(plugin => plugin.Type == pluginType && plugin.Id == pluginId && !plugin.IsInternal);

    /// <summary>
    /// Collects the metadata an archive declares about itself, together with the information about
    /// the installed plugin it would replace.
    /// </summary>
    /// <param name="plugin">The validated plugin from the archive.</param>
    /// <returns>The preview shown to the user before the installation starts.</returns>
    private static PluginImportPreview CreateImportPreview(PluginBase plugin) => new(
        plugin,
        FindReplaceablePlugin(plugin.Id, plugin.Type),
        plugin is PluginConfiguration configurationPlugin ? CreateConfigurationImportSummary(configurationPlugin) : null);

    /// <summary>
    /// Collects what a configuration plugin would set up once it is installed.
    /// </summary>
    /// <remarks>
    /// The plugin was loaded as a dry run, so nothing of this is stored yet. The destinations come
    /// from the parsed configuration objects, which is why the preview can name the host a provider
    /// would talk to.
    /// </remarks>
    private static ConfigurationPluginImportSummary CreateConfigurationImportSummary(PluginConfiguration configurationPlugin)
    {
        var configObjects = configurationPlugin.ConfigObjects.ToList();
        var destinations = configObjects
            .Where(configObject => configObject.Type is PluginConfigurationObjectType.LLM_PROVIDER
                or PluginConfigurationObjectType.EMBEDDING_PROVIDER
                or PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER
                or PluginConfigurationObjectType.DATA_SOURCE)
            .Select(configObject => new ConfigurationPluginDestination(configObject.Type, configObject.Name, configObject.Endpoint))
            .ToList();

        return new(
            Destinations: destinations,
            ChatTemplates: CountObjects(PluginConfigurationObjectType.CHAT_TEMPLATE),
            Profiles: CountObjects(PluginConfigurationObjectType.PROFILE),
            DocumentAnalysisPolicies: CountObjects(PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY),
            DeclaredSettings: configurationPlugin.DeclaredSettingsCount,
            MandatoryInfos: configurationPlugin.MandatoryInfos.Count,
            Introductions: configurationPlugin.Introductions.Count);

        int CountObjects(PluginConfigurationObjectType type) => configObjects.Count(configObject => configObject.Type == type);
    }

    /// <summary>
    /// Checks whether an installation may replace the plugin that currently uses the given ID.
    /// Plugins deployed by a Config Server belong to the organization's IT, so neither an import nor
    /// the Assistant Builder may overwrite them.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin about to be installed.</param>
    /// <param name="pluginType">The type of the plugin about to be installed.</param>
    /// <returns>A user-facing issue when the existing plugin must not be replaced, an empty string otherwise.</returns>
    private static string GetReplacementIssue(Guid pluginId, PluginType pluginType)
    {
        var existingPlugin = FindReplaceablePlugin(pluginId, pluginType);
        if (existingPlugin is null)
            return string.Empty;

        if (existingPlugin.IsManagedByConfigServer)
            return TB("Plugins deployed by your organization cannot be replaced.");

        if (string.IsNullOrWhiteSpace(existingPlugin.LocalPath))
            return string.Empty;

        // The metadata above and the running plugin read the same Lua field. We check both, though,
        // just like the deletion path does:
        var runningPlugin = PluginFactory.RunningPlugins
            .FirstOrDefault(candidate => candidate.Id == pluginId && IsSameDirectory(candidate.PluginPath, existingPlugin.LocalPath));

        var isManagedByConfigServer = runningPlugin switch
        {
            PluginAssistants assistantPlugin => assistantPlugin.IsManagedByConfigServer,
            PluginConfiguration configurationPlugin => configurationPlugin.DeployedUsingConfigServer ?? false,

            _ => false,
        };

        return isManagedByConfigServer
            ? TB("Plugins deployed by your organization cannot be replaced.")
            : string.Empty;
    }

    private static string CreateInstallBackupDirectory(IPluginMetadata plugin)
    {
        var backupRoot = Path.Join(SettingsManager.DataDirectory, INSTALL_BACKUP_DIRECTORY);
        return Path.Join(backupRoot, $"assistant-{plugin.Id:N}-{Guid.NewGuid():N}");
    }

    private static string CreatePluginDirectoryName(IPluginMetadata plugin)
    {
        var safeName = CreateSafeDirectoryNamePart(plugin.Name);
        return $"{safeName}-{plugin.Id:N}";
    }

    private static string CreateSafeDirectoryNamePart(string name)
    {
        var sb = new StringBuilder();
        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();

        foreach (var character in name.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                sb.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (character is '-' or '_' or '.' && !invalidChars.Contains(character))
            {
                sb.Append(character);
                continue;
            }

            AppendSeparator();
        }

        var safeName = sb.ToString().Trim('-', '.');
        if (safeName.Length > DIRECTORY_PREFIX_MAX_LEN)
            safeName = safeName[..DIRECTORY_PREFIX_MAX_LEN].Trim('-', '.');

        // Fallback for a plugin name without any usable character. The plugin ID is appended by the
        // caller, so the directory stays unique either way:
        return string.IsNullOrWhiteSpace(safeName)
            ? "plugin"
            : safeName;

        void AppendSeparator()
        {
            if (sb.Length == 0 || sb[^1] == '-')
                return;

            sb.Append('-');
        }
    }
}