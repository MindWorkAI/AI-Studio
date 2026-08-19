using System.Text;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed partial class PluginInstallService
{
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

        if (!TryGetPluginRoot(PluginType.ASSISTANT, out var assistantPluginsRoot, out var rootIssue))
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

        if (!TryGetPluginRoot(PluginType.ASSISTANT, out var assistantPluginsRoot, out var rootIssue))
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

        // We reload the plugins ourselves below. Holding back hot reloading keeps the file system
        // watcher from starting a second reload while the plugin file is being replaced:
        await PluginFactory.LockHotReloadAsync();
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

            PluginFactory.UnlockHotReload();
            this.installSemaphore.Release();
        }
    }

    private async Task<PluginValidationResult> ValidateInPluginDirectoryAsync(string lua, string pluginDirectory, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(lua))
            return PluginValidationResult.Failure(TB("No Lua plugin code was generated."));

        if (!PluginFactory.IsInitialized)
            return PluginValidationResult.Failure(TB("The plugin system is not initialized yet."));

        try
        {
            return await ValidatePluginCodeAsync(
                pluginDirectory,
                lua.Trim(),
                [PluginType.ASSISTANT],
                TB("The edited plugin is not an assistant plugin. Issue: {0}"),
                TB("The edited assistant plugin is invalid. Issue: {0}"),
                TB("The edited assistant plugin uses the ID of another installed plugin."),
                token);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to validate edited assistant plugin.");
            return PluginValidationResult.Failure(string.Format(TB("Unexpected error: {0}"), e.Message));
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
}