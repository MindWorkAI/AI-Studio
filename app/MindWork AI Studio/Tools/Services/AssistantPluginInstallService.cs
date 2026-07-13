using System.Text;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools.Services;

public sealed record AssistantPluginInstallResult(bool Success, Guid PluginId, string PluginName, string PluginDirectory, bool ReplacedExisting, string Issue);

public sealed record AssistantPluginCheckResult(bool Success, Guid PluginId, string PluginName, string Issue);

public sealed record AssistantPluginDeleteResult(bool Success, Guid PluginId, string PluginName, string PluginDirectory, string Issue);

public sealed record AssistantPluginUpdateResult(bool Success, Guid PluginId, string PluginName, string PluginDirectory, string Issue);

public sealed class AssistantPluginInstallService
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantPluginInstallService).Namespace, nameof(AssistantPluginInstallService));
    
    private const string PLUGIN_FILE_NAME = "plugin.lua";
    private const string ASSISTANT_BUILDER_DIRECTORY_PREFIX = "assistant-builder";
    private const string DELETE_BACKUP_DIRECTORY = ".plugin-delete-backups";
    private const int DIRECTORY_PREFIX_MAX_LEN = 80;
    
    private readonly ILogger<AssistantPluginInstallService> logger;
    private readonly SettingsManager settingsManager;
    private readonly SemaphoreSlim installSemaphore = new(1, 1);
    
    private static AssistantPluginInstallResult Error(string issue) => new(false, Guid.Empty, string.Empty, string.Empty, false, issue);
    
    private static AssistantPluginCheckResult CheckError(string issue) => new(false, Guid.Empty, string.Empty, issue);
    
    private static AssistantPluginDeleteResult DeleteError(IPluginMetadata plugin, string pluginDirectory, string issue) => new(false, plugin.Id, plugin.Name, pluginDirectory, issue);

    private static AssistantPluginUpdateResult UpdateError(IPluginMetadata plugin, string pluginDirectory, string issue) => new(false, plugin.Id, plugin.Name, pluginDirectory, issue);

    public AssistantPluginInstallService(ILogger<AssistantPluginInstallService> logger, SettingsManager settingsManager)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.logger.LogInformation("The assistant plugin install service has been initialized.");
    }

    /// <summary>
    /// Checks whether generated Lua assistant plugin code can be loaded and installed.
    /// The plugin is written to a temporary staging directory and validated through the
    /// normal plugin loader, but it is not moved into the user plugin directory.
    /// </summary>
    /// <param name="lua">The full generated <c>plugin.lua</c> content.</param>
    /// <param name="token">A cancellation token for file IO and Lua validation.</param>
    /// <returns>
    /// Check result that contains success state, plugin metadata, and a user-facing issue when validation failed.
    /// </returns>
    public async Task<AssistantPluginCheckResult> CheckInstallabilityAsync(string lua, CancellationToken token)
    {
        if (!TryGetAssistantPluginsRoot(out var assistantPluginsRoot, out var rootIssue))
            return CheckError(rootIssue);

        await this.installSemaphore.WaitAsync(token);
        var stagingDirectory = string.Empty;
        try
        {
            var validation = await this.ValidateIntoStagingAsync(lua, token);
            if (!validation.Success || validation.AssistantPlugin is null)
                return CheckError(validation.Issue);

            stagingDirectory = validation.StagingDirectory;
            var finalDirectory = DetermineFinalDirectory(assistantPluginsRoot, validation.AssistantPlugin);
            if (!IsPathInsideDirectory(assistantPluginsRoot, finalDirectory))
                return CheckError(TB("The resolved plugin directory is outside the assistant plugin directory."));

            return new(true, validation.AssistantPlugin.Id, validation.AssistantPlugin.Name, string.Empty);
        }
        finally
        {
            this.TryDeleteStagingDirectory(stagingDirectory);
            this.installSemaphore.Release();
        }
    }

    /// <summary>
    /// Installs generated Lua assistant plugin code into the user plugin directory.
    /// Writes the plugin into a temporary staging directory first, validates it through the
    /// normal plugin loader, then moves into <c>data/plugins/assistants</c>.
    /// If plugin with same ID already exists, the existing directory is moved
    /// aside as backup and restored when replacement fails.
    /// </summary>
    /// <param name="lua">The full generated <c>plugin.lua</c> content.</param>
    /// <param name="token">A cancellation token for file IO, Lua validation, and plugin reload.</param>
    /// <returns>
    /// Installation result that contains success state, installed plugin metadata, final directory,
    /// whether an existing plugin was replaced, and user-facing issue when installation failed.
    /// </returns>
    public async Task<AssistantPluginInstallResult> InstallAsync(string lua, CancellationToken token)
    {
        if (!TryGetAssistantPluginsRoot(out var assistantPluginsRoot, out var rootIssue))
            return Error(rootIssue);

        await this.installSemaphore.WaitAsync(token);
        AssistantPluginValidationResult validation;
        try
        {
            validation = await this.ValidateIntoStagingAsync(lua, token);
            if (!validation.Success || validation.AssistantPlugin is null)
                return Error(validation.Issue);

            Directory.CreateDirectory(assistantPluginsRoot);

            var stagingDirectory = validation.StagingDirectory;
            var assistantPlugin = validation.AssistantPlugin;
            string? backupDirectory = null;
            string? finalDirectory = null;
            var replacedExisting = false;

            try
            {
                finalDirectory = DetermineFinalDirectory(assistantPluginsRoot, assistantPlugin);
                if (!IsPathInsideDirectory(assistantPluginsRoot, finalDirectory))
                    return Error(TB("The resolved plugin directory is outside the assistant plugin directory."));

                if (Directory.Exists(finalDirectory))
                {
                    replacedExisting = true;
                    backupDirectory = Path.Join(assistantPluginsRoot, $".{Path.GetFileName(finalDirectory)}.backup-{Guid.NewGuid():N}");
                    Directory.Move(finalDirectory, backupDirectory);
                }

                Directory.Move(stagingDirectory, finalDirectory);
                if (!string.IsNullOrWhiteSpace(backupDirectory) && Directory.Exists(backupDirectory))
                {
                    try
                    {
                        Directory.Delete(backupDirectory, true);
                    }
                    catch (Exception e)
                    {
                        this.logger.LogError(e, $"Failed to delete assistant plugin backup directory '{backupDirectory}'.");
                    }
                }

                await PluginFactory.LoadAll(token);
                this.logger.LogInformation($"Installed assistant plugin '{assistantPlugin.Name}' ({assistantPlugin.Id}) to '{finalDirectory}'.");
                return new(true, assistantPlugin.Id, assistantPlugin.Name, finalDirectory, replacedExisting, string.Empty);
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Failed to install assistant plugin.");

                if (!string.IsNullOrWhiteSpace(backupDirectory) && Directory.Exists(backupDirectory) && !string.IsNullOrWhiteSpace(finalDirectory) && !Directory.Exists(finalDirectory))
                {
                    try
                    {
                        Directory.Move(backupDirectory, finalDirectory);
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
        finally
        {
            this.installSemaphore.Release();
        }
    }

    /// <summary>
    /// Checks whether edited assistant plugin code can replace an installed local assistant plugin
    /// without writing the file.
    /// </summary>
    /// <param name="plugin">The installed local assistant plugin to validate against.</param>
    /// <param name="lua">The edited <c>plugin.lua</c> content.</param>
    /// <param name="token">Cancellation token for Lua validation.</param>
    /// <returns>Check result that contains success state, plugin metadata, and a user-facing issue when validation failed.</returns>
    public async Task<AssistantPluginCheckResult> CheckInstalledAssistantUpdateAsync(IAvailablePlugin plugin, string lua, CancellationToken token)
    {
        if (plugin.Type is not PluginType.ASSISTANT)
            return CheckError(TB("Only assistant plugins can be edited."));

        if (plugin.IsInternal)
            return CheckError(TB("Internal assistant plugins cannot be edited."));

        if (string.IsNullOrWhiteSpace(plugin.LocalPath))
            return CheckError(TB("The assistant plugin has no local directory."));

        if (!TryGetAssistantPluginsRoot(out var assistantPluginsRoot, out var rootIssue))
            return CheckError(rootIssue);

        var pluginDirectory = plugin.LocalPath;
        if (!IsPathInsideDirectory(assistantPluginsRoot, pluginDirectory) || IsSameDirectory(assistantPluginsRoot, pluginDirectory))
            return CheckError(TB("The assistant plugin directory is outside the local assistant plugin directory."));

        if (!Directory.Exists(pluginDirectory))
            return CheckError(TB("The assistant plugin directory does not exist."));

        await this.installSemaphore.WaitAsync(token);
        try
        {
            var validation = await this.ValidateInPluginDirectoryAsync(lua, pluginDirectory, token);
            if (!validation.Success || validation.AssistantPlugin is null)
                return CheckError(validation.Issue);

            var assistantPlugin = validation.AssistantPlugin;
            return assistantPlugin.Id != plugin.Id
                ? CheckError(TB("The edited assistant plugin must keep the same plugin ID."))
                : new(true, assistantPlugin.Id, assistantPlugin.Name, string.Empty);
        }
        finally
        {
            this.installSemaphore.Release();
        }
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
    public async Task<AssistantPluginDeleteResult> DeleteInstalledAssistantAsync(IAvailablePlugin plugin, CancellationToken token)
    {
        if (plugin.Type is not PluginType.ASSISTANT)
            return DeleteError(plugin, plugin.LocalPath, TB("Only assistant plugins can be deleted."));

        if (plugin.IsInternal)
            return DeleteError(plugin, plugin.LocalPath, TB("Internal assistant plugins cannot be deleted."));

        if (string.IsNullOrWhiteSpace(plugin.LocalPath))
            return DeleteError(plugin, string.Empty, TB("The assistant plugin has no local directory."));

        if (!TryGetAssistantPluginsRoot(out var assistantPluginsRoot, out var rootIssue))
            return DeleteError(plugin, plugin.LocalPath, rootIssue);

        var pluginDirectory = plugin.LocalPath;
        if (!IsPathInsideDirectory(assistantPluginsRoot, pluginDirectory) || IsSameDirectory(assistantPluginsRoot, pluginDirectory))
            return DeleteError(plugin, pluginDirectory, TB("The assistant plugin directory is outside the local assistant plugin directory."));

        if (!Directory.Exists(pluginDirectory))
            return DeleteError(plugin, pluginDirectory, TB("The assistant plugin directory does not exist."));

        await this.installSemaphore.WaitAsync(token);
        var backupDirectory = string.Empty;
        var wasEnabled = false;
        var removedAudits = new List<PluginAssistantAudit>();

        try
        {
            backupDirectory = CreateDeleteBackupDirectory(plugin);
            Directory.CreateDirectory(Path.GetDirectoryName(backupDirectory)!);
            Directory.Move(pluginDirectory, backupDirectory);

            wasEnabled = this.settingsManager.ConfigurationData.EnabledPlugins.Remove(plugin.Id);
            removedAudits = this.settingsManager.ConfigurationData.AssistantPluginAudits
                .Where(audit => audit.PluginId == plugin.Id)
                .ToList();

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
    /// Updates installed assistant plugin <c>plugin.lua</c> file.
    /// The edited Lua code is validated from the provided string before it is written,
    /// but validation uses existing plugin directory as loader context so
    /// <c>require(...)</c> can resolve companion files such as <c>icon.lua</c>.
    /// After successful validation, the current <c>plugin.lua</c> is backed up,
    /// replaced atomically through a temporary file in the plugin directory, and
    /// restored when the plugin reload fails.
    /// </summary>
    /// <param name="plugin">The installed local assistant plugin to update.</param>
    /// <param name="lua">The edited <c>plugin.lua</c> content.</param>
    /// <param name="token">Cancellation token for Lua validation, file IO, and plugin reload.</param>
    /// <returns>
    /// Update result that contains success state, updated plugin metadata, the plugin directory,
    /// and a user-facing issue when the update failed.
    /// </returns>
    public async Task<AssistantPluginUpdateResult> UpdateInstalledAssistantAsync(IAvailablePlugin plugin, string lua, CancellationToken token)
    {
        if (plugin.Type is not PluginType.ASSISTANT)
            return UpdateError(plugin, plugin.LocalPath, TB("Only assistant plugins can be edited."));

        if (plugin.IsInternal)
            return UpdateError(plugin, plugin.LocalPath, TB("Internal assistant plugins cannot be edited."));

        if (string.IsNullOrWhiteSpace(plugin.LocalPath))
            return UpdateError(plugin, string.Empty, TB("The assistant plugin has no local directory."));

        if (!TryGetAssistantPluginsRoot(out var assistantPluginsRoot, out var rootIssue))
            return UpdateError(plugin, plugin.LocalPath, rootIssue);

        var pluginDirectory = plugin.LocalPath;
        if (!IsPathInsideDirectory(assistantPluginsRoot, pluginDirectory) || IsSameDirectory(assistantPluginsRoot, pluginDirectory))
            return UpdateError(plugin, pluginDirectory, TB("The assistant plugin directory is outside the local assistant plugin directory."));

        if (!Directory.Exists(pluginDirectory))
            return UpdateError(plugin, pluginDirectory, TB("The assistant plugin directory does not exist."));

        var pluginFile = Path.Join(pluginDirectory, PLUGIN_FILE_NAME);
        if (!IsPathInsideDirectory(pluginDirectory, pluginFile))
            return UpdateError(plugin, pluginDirectory, TB("The plugin file is outside the assistant plugin directory."));

        await this.installSemaphore.WaitAsync(token);
        var tempFile = string.Empty;
        var backupFile = string.Empty;

        try
        {
            var validation = await this.ValidateInPluginDirectoryAsync(lua, pluginDirectory, token);
            if (!validation.Success || validation.AssistantPlugin is null)
                return UpdateError(plugin, pluginDirectory, validation.Issue);

            var assistantPlugin = validation.AssistantPlugin;
            if (assistantPlugin.Id != plugin.Id)
                return UpdateError(plugin, pluginDirectory, TB("The edited assistant plugin must keep the same plugin ID."));

            var pluginCode = lua.Trim();
            tempFile = Path.Join(pluginDirectory, $"{PLUGIN_FILE_NAME}.tmp-{Guid.NewGuid():N}");
            backupFile = Path.Join(pluginDirectory, $"{PLUGIN_FILE_NAME}.backup-{Guid.NewGuid():N}");

            await File.WriteAllTextAsync(tempFile, pluginCode, Encoding.UTF8, token);

            if (File.Exists(pluginFile))
                File.Replace(tempFile, pluginFile, backupFile);
            else
                File.Move(tempFile, pluginFile);

            try
            {
                await PluginFactory.LoadAll(token);
                if (File.Exists(backupFile))
                    File.Delete(backupFile);

                this.logger.LogInformation($"Updated assistant plugin '{assistantPlugin.Name}' ({assistantPlugin.Id}) at '{pluginFile}'.");
                return new(true, assistantPlugin.Id, assistantPlugin.Name, pluginDirectory, string.Empty);
            }
            catch (Exception reloadException)
            {
                this.logger.LogError(reloadException, $"Failed to reload plugins after editing assistant plugin '{plugin.Name}' ({plugin.Id}).");
                await this.TryRestoreEditedAssistantPluginAsync(pluginFile, backupFile, token);
                return UpdateError(plugin, pluginDirectory, string.Format(TB("Unexpected error: {0}"), reloadException.Message));
            }
        }
        catch (Exception e)
        {
            this.logger.LogError(e, $"Failed to update assistant plugin '{plugin.Name}' ({plugin.Id}) at '{pluginDirectory}'.");
            await this.TryRestoreEditedAssistantPluginAsync(pluginFile, backupFile, token);
            return UpdateError(plugin, pluginDirectory, string.Format(TB("Unexpected error: {0}"), e.Message));
        }
        finally
        {
            this.TryDeleteFile(tempFile, "assistant plugin edit temp file");

            this.installSemaphore.Release();
        }
    }

    private async Task<AssistantPluginValidationResult> ValidateIntoStagingAsync(string lua, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(lua))
            return AssistantPluginValidationResult.Failure(TB("No Lua plugin code was generated."));

        if (!PluginFactory.IsInitialized)
            return AssistantPluginValidationResult.Failure(TB("The plugin system is not initialized yet."));

        var pluginCode = lua.Trim();
        var stagingDirectory = Path.Join(Path.GetTempPath(), $"{ASSISTANT_BUILDER_DIRECTORY_PREFIX}.staging-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            var stagedPluginFile = Path.Join(stagingDirectory, PLUGIN_FILE_NAME);
            await File.WriteAllTextAsync(stagedPluginFile, pluginCode, Encoding.UTF8, token);

            var validation = await this.ValidateAssistantPluginCodeAsync(
                stagingDirectory,
                pluginCode,
                TB("The generated plugin is not an assistant plugin. Issue: {0}"),
                TB("The generated assistant plugin is invalid. Issue: {0}"),
                TB("The generated assistant plugin uses the ID of an internal AI Studio plugin."),
                token);

            if (!validation.Success || validation.AssistantPlugin is null)
                this.TryDeleteStagingDirectory(stagingDirectory);

            return validation with { StagingDirectory = stagingDirectory };
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to validate generated assistant plugin.");
            this.TryDeleteStagingDirectory(stagingDirectory);
            return AssistantPluginValidationResult.Failure(string.Format(TB("Unexpected error: {0}"), e.Message));
        }
    }

    private async Task<AssistantPluginValidationResult> ValidateInPluginDirectoryAsync(string lua, string pluginDirectory, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(lua))
            return AssistantPluginValidationResult.Failure(TB("No Lua plugin code was generated."));

        if (!PluginFactory.IsInitialized)
            return AssistantPluginValidationResult.Failure(TB("The plugin system is not initialized yet."));

        try
        {
            return await this.ValidateAssistantPluginCodeAsync(
                pluginDirectory,
                lua.Trim(),
                TB("The edited plugin is not an assistant plugin. Issue: {0}"),
                TB("The edited assistant plugin is invalid. Issue: {0}"),
                TB("The edited assistant plugin uses the ID of an internal AI Studio plugin."),
                token);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to validate edited assistant plugin.");
            return AssistantPluginValidationResult.Failure(string.Format(TB("Unexpected error: {0}"), e.Message));
        }
    }

    private async Task<AssistantPluginValidationResult> ValidateAssistantPluginCodeAsync(
        string pluginDirectory,
        string pluginCode,
        string notAssistantIssue,
        string invalidAssistantIssue,
        string internalPluginIdIssue,
        CancellationToken token)
    {
        var plugin = await PluginFactory.Load(pluginDirectory, pluginCode, token);
        if (plugin is not PluginAssistants assistantPlugin)
            return AssistantPluginValidationResult.Failure(string.Format(notAssistantIssue, string.Join("; ", plugin.Issues)));

        if (!assistantPlugin.IsValid)
            return AssistantPluginValidationResult.Failure(string.Format(invalidAssistantIssue, string.Join("; ", assistantPlugin.Issues)));

        if (PluginFactory.AvailablePlugins.Any(availablePlugin => availablePlugin.Type is PluginType.ASSISTANT && availablePlugin.Id == assistantPlugin.Id && availablePlugin.IsInternal))
            return AssistantPluginValidationResult.Failure(internalPluginIdIssue);

        return new(true, string.Empty, assistantPlugin, string.Empty);
    }

    private static bool TryGetAssistantPluginsRoot(out string assistantPluginsRoot, out string issue)
    {
        assistantPluginsRoot = string.Empty;
        issue = string.Empty;

        var dataDirectory = SettingsManager.DataDirectory;
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            issue = TB("The AI Studio data directory is not initialized yet.");
            return false;
        }

        assistantPluginsRoot = Path.Join(dataDirectory, "plugins", PluginType.ASSISTANT.GetDirectory());
        return true;
    }

    private void TryDeleteStagingDirectory(string stagingDirectory)
    {
        TryDeleteDirectory(stagingDirectory, "assistant plugin staging", this.logger);
    }
    
    private static string DetermineFinalDirectory(string assistantPluginsRoot, PluginAssistants assistantPlugin)
    {
        var existingPlugin = PluginFactory.AvailablePlugins
            .OfType<IAvailablePlugin>()
            .FirstOrDefault(plugin => plugin.Type is PluginType.ASSISTANT && plugin.Id == assistantPlugin.Id && !plugin.IsInternal);

        return existingPlugin is not null
            ? existingPlugin.LocalPath
            : Path.Join(assistantPluginsRoot, CreatePluginDirectoryName(assistantPlugin));
    }

    private static string CreatePluginDirectoryName(PluginAssistants assistantPlugin)
    {
        var safeName = CreateSafeDirectoryNamePart(assistantPlugin.Name);
        return $"{safeName}-{assistantPlugin.Id:N}";
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

        return string.IsNullOrWhiteSpace(safeName)
            ? ASSISTANT_BUILDER_DIRECTORY_PREFIX
            : safeName;

        void AppendSeparator()
        {
            if (sb.Length == 0 || sb[^1] == '-')
                return;

            sb.Append('-');
        }
    }

    private static bool IsPathInsideDirectory(string parentDirectory, string path)
    {
        var parentPath = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var childPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameDirectory(string firstDirectory, string secondDirectory)
    {
        var firstPath = Path.GetFullPath(firstDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var secondPath = Path.GetFullPath(secondDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateDeleteBackupDirectory(IAvailablePlugin plugin)
    {
        var backupRoot = Path.Join(SettingsManager.DataDirectory, DELETE_BACKUP_DIRECTORY);
        return Path.Join(backupRoot, $"assistant-{plugin.Id:N}-{Guid.NewGuid():N}");
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

    private async Task TryRestoreEditedAssistantPluginAsync(string pluginFile, string backupFile, CancellationToken token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupFile) || !File.Exists(backupFile))
                return;

            if (File.Exists(pluginFile))
                File.Delete(pluginFile);

            File.Move(backupFile, pluginFile);
            await PluginFactory.LoadAll(token);
        }
        catch (Exception restoreException)
        {
            this.logger.LogError(restoreException, $"Failed to restore assistant plugin file '{pluginFile}' after a failed edit.");
        }
    }

    private static void TryDeleteDirectory(string directory, string directoryDescription, ILogger logger)
    {
        if (!Directory.Exists(directory))
            return;

        try
        {
            Directory.Delete(directory, true);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to delete {directoryDescription} directory '{directory}'.");
        }
    }

    private void TryDeleteFile(string filePath, string fileDescription)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        try
        {
            File.Delete(filePath);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, $"Failed to delete {fileDescription} '{filePath}'.");
        }
    }

    private sealed record AssistantPluginValidationResult(bool Success, string StagingDirectory, PluginAssistants? AssistantPlugin, string Issue)
    {
        public static AssistantPluginValidationResult Failure(string issue) => new(false, string.Empty, null, issue);
    }
}
