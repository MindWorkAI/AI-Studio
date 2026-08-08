using System.Text;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class PluginInstallService
{
    /// <summary>
    /// Installs an assistant plugin archive that contains exactly one <c>plugin.lua</c> file.
    /// Companion files are validated from and moved with the same staging directory.
    /// </summary>
    /// <param name="archivePath">The local <c>.mwplugin</c> or <c>.zip</c> archive path.</param>
    /// <param name="confirmAsync">
    /// Asks the user whether the validated archive may be installed. It is called after all checks
    /// passed and before anything gets written. Returning false aborts the installation.
    /// </param>
    /// <param name="token">Cancellation token for extraction, validation, file IO, and plugin reload.</param>
    /// <returns>Installation result that contains success state, installed plugin metadata, and a user-facing issue when installation failed.</returns>
    public async Task<AssistantPluginInstallResult> InstallArchiveAsync(string archivePath, Func<PluginImportPreview, Task<bool>> confirmAsync, CancellationToken token)
    {
        if (!this.settingsManager.ConfigurationData.App.AllowUserToImportPlugins)
            return Error(TB("Your organization has disabled importing plugins."));

        if (!FileTypes.IsAllowedPath(archivePath, FileTypes.PLUGIN_ARCHIVE))
            return Error(TB("Please select a plugin archive with the extension .mwplugin or .zip."));

        if (!File.Exists(archivePath))
            return Error(TB("The selected plugin archive does not exist."));

        if (!TryGetPluginRoot(PluginType.ASSISTANT, out var assistantPluginsRoot, out var rootIssue))
            return Error(rootIssue);

        if (!PluginFactory.IsInitialized)
            return Error(TB("The plugin system is not initialized yet."));

        await this.installSemaphore.WaitAsync(token);
        var stagingDirectory = Path.Join(Path.GetTempPath(), $"assistant-plugin-import.staging-{Guid.NewGuid():N}");
        try
        {
            token.ThrowIfCancellationRequested();
            PluginArchive.Extract(archivePath, stagingDirectory);

            var pluginFiles = Directory.EnumerateFiles(stagingDirectory, PLUGIN_FILE_NAME, SearchOption.AllDirectories).ToArray();
            if (pluginFiles.Length != 1)
                return Error(TB("The plugin archive must contain exactly one plugin.lua file."));

            var pluginFile = pluginFiles[0];
            var pluginDirectory = Path.GetDirectoryName(pluginFile)!;
            var pluginCode = await File.ReadAllTextAsync(pluginFile, Encoding.UTF8, token);
            var validation = await ValidatePluginCodeAsync(
                pluginDirectory,
                pluginCode.Trim(),
                PluginType.ASSISTANT,
                TB("Currently, only assistant plugins can be imported."),
                TB("The imported assistant plugin is invalid. Issue: {0}"),
                TB("The imported assistant plugin uses the ID of another installed plugin."),
                token);

            if (!validation.Success || validation.AssistantPlugin is null)
                return Error(validation.Issue);

            // A plugin the user imports by hand never comes from a config server. We reject such
            // archives because AI Studio trusts this self-declared flag: an imported plugin
            // claiming it would be neither replaceable nor deletable through the user interface:
            if (validation.AssistantPlugin.IsManagedByConfigServer)
                return Error(TB("This plugin archive declares itself as managed by a config server. Only the IT department of your organization might deploy such plugins."));

            // The archive would replace an existing plugin: reject it when that plugin belongs
            // to the IT department. We check this before asking the user, so that the
            // confirmation never offers something we would refuse afterwards anyway:
            var replacementIssue = GetReplacementIssue(validation.AssistantPlugin.Id, PluginType.ASSISTANT);
            if (!string.IsNullOrEmpty(replacementIssue))
                return Error(replacementIssue);

            // Everything is validated, but nothing was written yet. This is the point where the
            // user decides, because the plugin code comes from an untrusted source:
            if (!await confirmAsync(CreateImportPreview(validation.AssistantPlugin)))
                return CancelledByUser();

            return await this.InstallStagedPluginAsync(assistantPluginsRoot, validation with { StagingDirectory = pluginDirectory }, PluginType.ASSISTANT, token);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            this.logger.LogError(e, "Failed to extract or validate assistant plugin archive '{ArchivePath}'.", archivePath);
            return Error(string.Format(TB("Unexpected error: {0}"), e.Message));
        }
        finally
        {
            this.TryDeleteStagingDirectory(stagingDirectory);
            this.installSemaphore.Release();
        }
    }
}