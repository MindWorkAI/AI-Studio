using System.Text;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools.Services;

public sealed record AssistantPluginInstallResult(bool Success, Guid PluginId, string PluginName, string PluginDirectory, bool ReplacedExisting, string Issue);
public sealed record AssistantPluginCheckResult(bool Success, Guid PluginId, string PluginName, string Issue);

public sealed class AssistantPluginInstallService
{
    private const string PLUGIN_FILE_NAME = "plugin.lua";
    private const string ASSISTANT_BUILDER_DIRECTORY_PREFIX = "assistant-builder";
    private const int DIRECTORY_PREFIX_MAX_LEN = 80;
    
    private readonly ILogger<AssistantPluginInstallService> logger;
    private readonly SemaphoreSlim installSemaphore = new(1, 1);
    
    private static AssistantPluginInstallResult Error(string issue) => new(false, Guid.Empty, string.Empty, string.Empty, false, issue);
    private static AssistantPluginCheckResult CheckError(string issue) => new(false, Guid.Empty, string.Empty, issue);

    public AssistantPluginInstallService(ILogger<AssistantPluginInstallService> logger)
    {
        this.logger = logger;
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
                return CheckError("The resolved plugin directory is outside the assistant plugin directory.");

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
                this.TryDeleteStagingDirectory(stagingDirectory);
            }
        }
        finally
        {
            this.installSemaphore.Release();
        }
    }

    private async Task<AssistantPluginValidationResult> ValidateIntoStagingAsync(string lua, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(lua))
            return AssistantPluginValidationResult.Error("No Lua plugin code was generated.");

        if (!PluginFactory.IsInitialized)
            return AssistantPluginValidationResult.Error("The plugin system is not initialized yet.");

        var pluginCode = lua.Trim();
        var stagingDirectory = Path.Join(Path.GetTempPath(), $"{ASSISTANT_BUILDER_DIRECTORY_PREFIX}.staging-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            var stagedPluginFile = Path.Join(stagingDirectory, PLUGIN_FILE_NAME);
            await File.WriteAllTextAsync(stagedPluginFile, pluginCode, Encoding.UTF8, token);

            var plugin = await PluginFactory.Load(stagingDirectory, pluginCode, token);
            if (plugin is not PluginAssistants assistantPlugin)
            {
                this.TryDeleteStagingDirectory(stagingDirectory);
                return AssistantPluginValidationResult.Error($"The generated plugin is not an assistant plugin. Issue: {string.Join("; ", plugin.Issues)}");
            }

            if (!assistantPlugin.IsValid)
            {
                this.TryDeleteStagingDirectory(stagingDirectory);
                return AssistantPluginValidationResult.Error($"The generated assistant plugin is invalid. Issue: {string.Join("; ", assistantPlugin.Issues)}");
            }

            if (PluginFactory.AvailablePlugins.Any(availablePlugin => availablePlugin.Type is PluginType.ASSISTANT && availablePlugin.Id == assistantPlugin.Id && availablePlugin.IsInternal))
            {
                this.TryDeleteStagingDirectory(stagingDirectory);
                return AssistantPluginValidationResult.Error("The generated assistant plugin uses the ID of an internal AI Studio plugin.");
            }

            return new(true, stagingDirectory, assistantPlugin, string.Empty);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to validate generated assistant plugin.");
            this.TryDeleteStagingDirectory(stagingDirectory);
            return AssistantPluginValidationResult.Error(e.Message);
        }
    }

    private static bool TryGetAssistantPluginsRoot(out string assistantPluginsRoot, out string issue)
    {
        assistantPluginsRoot = string.Empty;
        issue = string.Empty;

        var dataDirectory = SettingsManager.DataDirectory;
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            issue = "The AI Studio data directory is not initialized yet.";
            return false;
        }

        assistantPluginsRoot = Path.Join(dataDirectory, "plugins", PluginType.ASSISTANT.GetDirectory());
        return true;
    }

    private void TryDeleteStagingDirectory(string stagingDirectory)
    {
        if (!Directory.Exists(stagingDirectory))
            return;

        try
        {
            Directory.Delete(stagingDirectory, true);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to delete assistant plugin staging directory '{StagingDirectory}'.", stagingDirectory);
        }
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

    private sealed record AssistantPluginValidationResult(bool Success, string StagingDirectory, PluginAssistants? AssistantPlugin, string Issue)
    {
        public static AssistantPluginValidationResult Error(string issue) => new(false, string.Empty, null, issue);
    }
}