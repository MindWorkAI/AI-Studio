using System.Text;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class PluginInstallService
{
    /// <summary>
    /// The plugin types a user may import from an archive.
    /// </summary>
    private static readonly PluginType[] IMPORTABLE_PLUGIN_TYPES = [PluginType.ASSISTANT, PluginType.CONFIGURATION];

    /// <summary>
    /// Installs a plugin archive that contains exactly one <c>plugin.lua</c> file.
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

        if (!PluginFactory.IsInitialized)
            return Error(TB("The plugin system is not initialized yet."));

        await this.installSemaphore.WaitAsync(token);
        var stagingDirectory = Path.Join(Path.GetTempPath(), $"plugin-import.staging-{Guid.NewGuid():N}");
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
                IMPORTABLE_PLUGIN_TYPES,
                TB("Only assistant and configuration plugins can be imported."),
                TB("The imported plugin is invalid. Issue: {0}"),
                TB("The imported plugin uses the ID of another installed plugin."),
                token);

            if (!validation.Success || validation.Plugin is null)
                return Error(validation.Issue);

            var plugin = validation.Plugin;
            var eligibilityIssue = this.GetImportEligibilityIssue(plugin);
            if (!string.IsNullOrEmpty(eligibilityIssue))
                return Error(eligibilityIssue);

            // The archive would replace an existing plugin: reject it when that plugin belongs
            // to the IT department. We check this before asking the user, so that the
            // confirmation never offers something we would refuse afterwards anyway:
            var replacementIssue = GetReplacementIssue(plugin.Id, plugin.Type);
            if (!string.IsNullOrEmpty(replacementIssue))
                return Error(replacementIssue);

            // Local plugins live in the directory of their type, never in the enterprise
            // configuration directory. Only a config server deploys plugins there:
            if (!TryGetPluginRoot(plugin.Type, out var pluginRoot, out var rootIssue))
                return Error(rootIssue);

            // Everything is validated, but nothing was written yet. This is the point where the
            // user decides, because the plugin code comes from an untrusted source:
            if (!await confirmAsync(CreateImportPreview(plugin)))
                return CancelledByUser();

            return await this.InstallStagedPluginAsync(pluginRoot, validation with { StagingDirectory = pluginDirectory }, plugin.Type, token);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            this.logger.LogError(e, "Failed to extract or validate plugin archive '{ArchivePath}'.", archivePath);
            return Error(string.Format(TB("Unexpected error: {0}"), e.Message));
        }
        finally
        {
            this.TryDeleteStagingDirectory(stagingDirectory);
            this.installSemaphore.Release();
        }
    }

    /// <summary>
    /// Checks the rules that depend on the type of the plugin inside the archive.
    /// </summary>
    /// <param name="plugin">The validated plugin from the archive.</param>
    /// <returns>A user-facing issue when the archive must not be installed, an empty string otherwise.</returns>
    private string GetImportEligibilityIssue(PluginBase plugin) => plugin switch
    {
        // A plugin the user imports by hand never comes from a config server. We reject such
        // archives because AI Studio trusts this self-declared flag: an imported plugin claiming it
        // would be neither replaceable nor deletable through the user interface:
        PluginAssistants { IsManagedByConfigServer: true } => TB("This plugin archive declares itself as managed by a config server. Only the IT department of your organization might deploy such plugins."),

        PluginConfiguration configurationPlugin => this.GetConfigurationImportEligibilityIssue(configurationPlugin),

        _ => string.Empty,
    };

    /// <summary>
    /// Checks the additional rules for importing a configuration plugin.
    /// </summary>
    /// <remarks>
    /// A configuration takes effect immediately and has no on/off switch, so it gets its own
    /// organization permission on top of the general import permission.
    /// </remarks>
    private string GetConfigurationImportEligibilityIssue(PluginConfiguration configurationPlugin)
    {
        if (!this.settingsManager.ConfigurationData.App.AllowUserToImportConfigurationPlugins)
            return TB("Your organization has disabled importing configuration plugins.");

        if (configurationPlugin.DeployedUsingConfigServer is true)
            return TB("This plugin archive declares itself as managed by a config server. Only the IT department of your organization might deploy such plugins.");

        // Never let an imported configuration take the place of one the organization deployed. This
        // also covers a deployed configuration which currently cannot be loaded, e.g. because of an
        // error in its Lua code:
        if (PluginFactory.IsEnterpriseConfigurationPlugin(configurationPlugin.Id))
            return TB("Your organization deployed a configuration with the same ID. An imported configuration must not take its place.");

        return string.Empty;
    }
}