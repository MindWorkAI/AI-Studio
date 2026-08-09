using System.Text;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed partial class PluginInstallService
{
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
        if (!TryGetPluginRoot(PluginType.ASSISTANT, out var assistantPluginsRoot, out var rootIssue))
            return CheckError(rootIssue);

        await this.installSemaphore.WaitAsync(token);
        var stagingDirectory = string.Empty;
        try
        {
            var validation = await this.ValidateIntoStagingAsync(lua, token);
            if (!validation.Success || validation.AssistantPlugin is null)
                return CheckError(validation.Issue);

            stagingDirectory = validation.StagingDirectory;
            var finalDirectory = DetermineFinalDirectory(assistantPluginsRoot, validation.AssistantPlugin, PluginType.ASSISTANT);
            if (!IsPathInsideDirectory(assistantPluginsRoot, finalDirectory))
                return CheckError(TB("The resolved plugin directory is outside the plugin directory."));

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
        if (!TryGetPluginRoot(PluginType.ASSISTANT, out var assistantPluginsRoot, out var rootIssue))
            return Error(rootIssue);

        await this.installSemaphore.WaitAsync(token);
        try
        {
            var validation = await this.ValidateIntoStagingAsync(lua, token);
            if (!validation.Success || validation.AssistantPlugin is null)
                return Error(validation.Issue);

            return await this.InstallStagedPluginAsync(assistantPluginsRoot, validation, PluginType.ASSISTANT, token);
        }
        finally
        {
            this.installSemaphore.Release();
        }
    }

    private async Task<PluginValidationResult> ValidateIntoStagingAsync(string lua, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(lua))
            return PluginValidationResult.Failure(TB("No Lua plugin code was generated."));

        if (!PluginFactory.IsInitialized)
            return PluginValidationResult.Failure(TB("The plugin system is not initialized yet."));

        var pluginCode = lua.Trim();
        var stagingDirectory = Path.Join(Path.GetTempPath(), $"{ASSISTANT_BUILDER_DIRECTORY_PREFIX}.staging-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            var stagedPluginFile = Path.Join(stagingDirectory, PLUGIN_FILE_NAME);
            await File.WriteAllTextAsync(stagedPluginFile, pluginCode, Encoding.UTF8, token);

            var validation = await ValidatePluginCodeAsync(
                stagingDirectory,
                pluginCode,
                [PluginType.ASSISTANT],
                TB("The generated plugin is not an assistant plugin. Issue: {0}"),
                TB("The generated assistant plugin is invalid. Issue: {0}"),
                TB("The generated assistant plugin uses the ID of another installed plugin."),
                token);

            if (!validation.Success || validation.AssistantPlugin is null)
                this.TryDeleteStagingDirectory(stagingDirectory);

            return validation with { StagingDirectory = stagingDirectory };
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to validate generated assistant plugin.");
            this.TryDeleteStagingDirectory(stagingDirectory);
            return PluginValidationResult.Failure(string.Format(TB("Unexpected error: {0}"), e.Message));
        }
    }
}