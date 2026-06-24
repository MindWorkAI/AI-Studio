using System.Text;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools.Services;

public sealed record AssistantPluginInstallResult(bool Success, Guid PluginId, string PluginName, string PluginDirectory, bool ReplacedExisting, string Issue);

public sealed class AssistantPluginInstallService
{
    private const string PLUGIN_FILE_NAME = "plugin.lua";
    private const string ASSISTANT_BUILDER_DIRECTORY_PREFIX = "assistant-builder";
    
    private readonly ILogger<AssistantPluginInstallService> logger;
    private readonly SemaphoreSlim installSemaphore = new(1, 1);
    
    private static AssistantPluginInstallResult Error(string issue) => new(false, Guid.Empty, string.Empty, string.Empty, false, issue);

    public AssistantPluginInstallService(ILogger<AssistantPluginInstallService> logger)
    {
        this.logger = logger;
        this.logger.LogInformation("The assistant plugin install service has been initialized.");
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
        if (string.IsNullOrWhiteSpace(lua))
            return Error("No Lua plugin code was generated.");

        var pluginCode = lua.Trim();
        if (!PluginFactory.IsInitialized)
            return Error("The plugin system is not initialized yet.");

        var dataDirectory = SettingsManager.DataDirectory;
        if (string.IsNullOrWhiteSpace(dataDirectory))
            return Error("The AI Studio data directory is not initialized yet.");

        await this.installSemaphore.WaitAsync(token);
        try
        {
            var assistantPluginsRoot = Path.Join(dataDirectory, "plugins", PluginType.ASSISTANT.GetDirectory());
            Directory.CreateDirectory(assistantPluginsRoot);

            var stagingDirectory = Path.Join(Path.GetTempPath(), $"{ASSISTANT_BUILDER_DIRECTORY_PREFIX}.staging-{Guid.NewGuid():N}");
            string? backupDirectory = null;
            string? finalDirectory = null;
            var replacedExisting = false;

            try
            {
                Directory.CreateDirectory(stagingDirectory);
                var stagedPluginFile = Path.Join(stagingDirectory, PLUGIN_FILE_NAME);
                await File.WriteAllTextAsync(stagedPluginFile, pluginCode, Encoding.UTF8, token);

                var plugin = await PluginFactory.Load(stagingDirectory, pluginCode, token);
                if (plugin is not PluginAssistants assistantPlugin)
                    return Error($"The generated plugin is not an assistant plugin. Issue: {string.Join("; ", plugin.Issues)}");

                if (!assistantPlugin.IsValid)
                    return Error($"The generated assistant plugin is invalid. Issue: {string.Join("; ", assistantPlugin.Issues)}");

                if (PluginFactory.AvailablePlugins.Any(plugin => plugin.Type is PluginType.ASSISTANT && plugin.Id == assistantPlugin.Id && plugin.IsInternal))
                    return Error("The generated assistant plugin uses the ID of an internal AI Studio plugin.");

                finalDirectory = DetermineFinalDirectory(assistantPluginsRoot, assistantPlugin);
                if (!IsPathInsideDirectory(assistantPluginsRoot, finalDirectory))
                    return Error("The resolved plugin directory is outside the assistant plugin directory.");

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
                        this.logger.LogError(e, "Failed to delete assistant plugin backup directory '{BackupDirectory}'.", backupDirectory);
                    }
                }

                await PluginFactory.LoadAll(token);
                this.logger.LogInformation("Installed assistant plugin '{PluginName}' ({PluginId}) to '{PluginDirectory}'.", assistantPlugin.Name, assistantPlugin.Id, finalDirectory);
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

                return Error(e.Message);
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                {
                    try
                    {
                        Directory.Delete(stagingDirectory, true);
                    }
                    catch (Exception e)
                    {
                        this.logger.LogError(e, "Failed to delete assistant plugin staging directory '{StagingDirectory}'.", stagingDirectory);
                    }
                }
            }
        }
        finally
        {
            this.installSemaphore.Release();
        }
    }
    
    private static string DetermineFinalDirectory(string assistantPluginsRoot, PluginAssistants assistantPlugin)
    {
        var existingPlugin = PluginFactory.AvailablePlugins
            .OfType<IAvailablePlugin>()
            .FirstOrDefault(plugin => plugin.Type is PluginType.ASSISTANT && plugin.Id == assistantPlugin.Id && !plugin.IsInternal);

        return existingPlugin is not null
            ? existingPlugin.LocalPath
            : Path.Join(assistantPluginsRoot, $"{ASSISTANT_BUILDER_DIRECTORY_PREFIX}-{assistantPlugin.Id:N}");
    }

    private static bool IsPathInsideDirectory(string parentDirectory, string path)
    {
        var parentPath = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var childPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
    }
}
