using System.Text.RegularExpressions;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private const string REASON_NO_LONGER_REFERENCED = "no longer referenced by active enterprise environments";

    public static void RemoveUnreferencedManagedConfigurationPlugins(ISet<Guid> activeConfigurationIds)
    {
        if (!IsInitialized)
            return;

        var pluginIdsToRemove = new HashSet<Guid>();

        // Case 1: Plugins are already loaded and metadata is available.
        foreach (var plugin in AVAILABLE_PLUGINS.Where(plugin =>
                     plugin.Type is PluginType.CONFIGURATION &&
                     plugin.IsManagedByConfigServer &&
                     !activeConfigurationIds.Contains(plugin.Id)))
            pluginIdsToRemove.Add(plugin.Id);

        // Case 2: Startup cleanup before the initial plugin load.
        // In this case, we inspect the .config directories directly.
        if (Directory.Exists(CONFIGURATION_PLUGINS_ROOT))
        {
            foreach (var pluginDirectory in Directory.EnumerateDirectories(CONFIGURATION_PLUGINS_ROOT))
            {
                var directoryName = Path.GetFileName(pluginDirectory);
                if (!Guid.TryParse(directoryName, out var pluginId))
                    continue;

                if (activeConfigurationIds.Contains(pluginId))
                    continue;

                var deployFlag = ReadDeployFlagFromPluginFile(pluginDirectory);
                var isManagedByConfigServer = deployFlag ?? true;
                if (!deployFlag.HasValue)
                    LOG.LogWarning($"Configuration plugin '{pluginId}' does not define 'DEPLOYED_USING_CONFIG_SERVER'. Falling back to the plugin path and treating it as managed because it is stored under '{CONFIGURATION_PLUGINS_ROOT}'.");

                if (isManagedByConfigServer)
                    pluginIdsToRemove.Add(pluginId);
            }
        }

        foreach (var pluginId in pluginIdsToRemove)
            RemovePluginAsync(pluginId, REASON_NO_LONGER_REFERENCED);
    }

    private static void RemovePluginAsync(Guid pluginId, string reason)
    {
        if (!IsInitialized)
            return;

        LOG.LogWarning("Removing plugin with ID '{PluginId}'. Reason: {Reason}.", pluginId, reason);

        //
        // Remove the plugin from the available plugins list:
        //
        var availablePluginToRemove = AVAILABLE_PLUGINS.FirstOrDefault(p => p.Id == pluginId);
        if (availablePluginToRemove != null)
            AVAILABLE_PLUGINS.Remove(availablePluginToRemove);
        else
            LOG.LogWarning("No available plugin found with ID '{PluginId}' while removing plugin. Reason: {Reason}.", pluginId, reason);
        
        //
        // Remove the plugin from the running plugins list:
        //
        var runningPluginToRemove = RUNNING_PLUGINS.FirstOrDefault(p => p.Id == pluginId);
        if (runningPluginToRemove == null)
            LOG.LogWarning("No running plugin found with ID '{PluginId}' while removing plugin. Reason: {Reason}.", pluginId, reason);
        else
            RUNNING_PLUGINS.Remove(runningPluginToRemove);

        //
        // Delete the plugin directory:
        //
        DeleteConfigurationPluginDirectory(pluginId);

        LOG.LogInformation("Plugin with ID '{PluginId}' removed successfully. Reason: {Reason}.", pluginId, reason);
    }

    private static bool? ReadDeployFlagFromPluginFile(string pluginDirectory)
    {
        try
        {
            var pluginFile = Path.Join(pluginDirectory, "plugin.lua");
            if (!File.Exists(pluginFile))
                return null;

            var pluginCode = File.ReadAllText(pluginFile);
            var match = DeployedByConfigServerRegex().Match(pluginCode);
            if (!match.Success)
                return null;

            return bool.TryParse(match.Groups[1].Value, out var deployFlag)
                ? deployFlag
                : null;
        }
        catch (Exception ex)
        {
            LOG.LogWarning(ex, $"Failed to parse deployment flag from plugin directory '{pluginDirectory}'.");
            return null;
        }
    }

    private static void DeleteConfigurationPluginDirectory(Guid pluginId)
    {
        var pluginDirectory = Path.Join(CONFIGURATION_PLUGINS_ROOT, pluginId.ToString());
        if (!Directory.Exists(pluginDirectory))
        {
            LOG.LogWarning($"Plugin directory '{pluginDirectory}' does not exist.");
            return;
        }

        try
        {
            Directory.Delete(pluginDirectory, true);
            LOG.LogInformation($"Plugin directory '{pluginDirectory}' deleted successfully.");
        }
        catch (Exception ex)
        {
            LOG.LogError(ex, $"Failed to delete plugin directory '{pluginDirectory}'.");
        }
    }

    [GeneratedRegex(@"^\s*DEPLOYED_USING_CONFIG_SERVER\s*=\s*(true|false)\s*(?:--.*)?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex DeployedByConfigServerRegex();
}