namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private const string REASON_NO_LONGER_REFERENCED = "no longer referenced by active enterprise environments";

    /// <summary>
    /// Removes the configuration plugins an organization deployed once but does not reference anymore.
    /// </summary>
    /// <remarks>
    /// This is how an organization withdraws a configuration: it removes the configuration ID from the
    /// devices, e.g. through a group policy. The next time AI Studio syncs, the local copy has to go.
    /// A device which was offline while the policy changed applies the withdrawal when it starts again.
    /// <br/><br/>
    /// What an organization deployed is decided by the plugin path alone. We must not ask the plugin
    /// itself: `DEPLOYED_USING_CONFIG_SERVER` is part of the plugin, so a configuration declaring
    /// `false` could never be withdrawn again once it was deployed, while it would keep every right of
    /// an organization configuration, including the approval of assistant plugins.
    /// </remarks>
    /// <param name="activeConfigurationIds">The IDs of the enterprise configurations which are currently referenced.</param>
    public static void RemoveUnreferencedManagedConfigurationPlugins(ISet<Guid> activeConfigurationIds)
    {
        if (!IsInitialized || !Directory.Exists(ENTERPRISE_CONFIGURATION_PLUGINS_ROOT))
            return;

        foreach (var configurationDirectory in Directory.EnumerateDirectories(ENTERPRISE_CONFIGURATION_PLUGINS_ROOT))
        {
            var directoryName = Path.GetFileName(configurationDirectory);

            // A download in flight stages and backs up next to the configuration directories. Those
            // directories belong to a running update, not to a withdrawn configuration:
            if (IsTransientDownloadDirectory(directoryName))
                continue;

            //
            // A configuration server downloads each configuration into a directory named after its
            // ID. Any other directory name cannot be referenced by an enterprise environment, so it
            // has no place here either:
            //
            if (Guid.TryParse(directoryName, out var configurationId) && activeConfigurationIds.Contains(configurationId))
                continue;

            RemoveConfigurationDirectory(configurationDirectory, REASON_NO_LONGER_REFERENCED);
        }
    }

    /// <summary>
    /// Checks whether a directory below the enterprise configuration directory belongs to a running
    /// download instead of to an installed configuration.
    /// </summary>
    private static bool IsTransientDownloadDirectory(string directoryName) =>
        directoryName.Contains(".staging-", StringComparison.OrdinalIgnoreCase) ||
        directoryName.Contains(".backup-", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Unloads every plugin stored in the given directory and deletes the directory afterwards.
    /// </summary>
    private static void RemoveConfigurationDirectory(string configurationDirectory, string reason)
    {
        LOG.LogWarning("Removing the enterprise configuration directory '{Directory}'. Reason: {Reason}.", configurationDirectory, reason);

        //
        // We collect the plugins by path, not by the ID the directory is named after: a plugin may
        // declare an ID which differs from its directory name, and a single directory may even hold
        // several plugins:
        //
        foreach (var plugin in AVAILABLE_PLUGINS.Where(plugin => IsPathInside(configurationDirectory, plugin.LocalPath)).ToList())
        {
            AVAILABLE_PLUGINS.Remove(plugin);

            if (RUNNING_PLUGINS.FirstOrDefault(runningPlugin => runningPlugin.Id == plugin.Id) is { } runningPluginToRemove)
            {
                RUNNING_PLUGINS.Remove(runningPluginToRemove);

                // The plugin is unloaded, so its Lua runtime is of no use anymore:
                runningPluginToRemove.Dispose();
            }

            LOG.LogInformation("Unloaded the plugin '{PluginName}' ({PluginId}). Reason: {Reason}.", plugin.Name, plugin.Id, reason);
        }

        if (!Directory.Exists(configurationDirectory))
            return;

        try
        {
            Directory.Delete(configurationDirectory, true);
            LOG.LogInformation($"Plugin directory '{configurationDirectory}' deleted successfully.");
        }
        catch (Exception e)
        {
            LOG.LogError(e, $"Failed to delete plugin directory '{configurationDirectory}'.");
        }
    }
}