namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    public static void RemovePluginAsync(Guid pluginId)
    {
        if (!IS_INITIALIZED)
            return;

        LOG.LogWarning($"Try to remove plugin with ID: {pluginId}");

        //
        // Remove the plugin from the available plugins list:
        //
        var availablePluginToRemove = AVAILABLE_PLUGINS.FirstOrDefault(p => p.Id == pluginId);
        if (availablePluginToRemove == null)
        {
            LOG.LogWarning($"No plugin found with ID: {pluginId}");
            return;
        }

        AVAILABLE_PLUGINS.Remove(availablePluginToRemove);
        
        //
        // Remove the plugin from the running plugins list:
        //
        var runningPluginToRemove = RUNNING_PLUGINS.FirstOrDefault(p => p.Id == pluginId);
        if (runningPluginToRemove == null)
            LOG.LogWarning($"No running plugin found with ID: {pluginId}");
        else
            RUNNING_PLUGINS.Remove(runningPluginToRemove);

        //
        // Delete the plugin directory:
        //
        var pluginDirectory = Path.Join(CONFIGURATION_PLUGINS_ROOT, availablePluginToRemove.Id.ToString());
        if (Directory.Exists(pluginDirectory))
        {
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
        else
            LOG.LogWarning($"Plugin directory '{pluginDirectory}' does not exist.");

        LOG.LogInformation($"Plugin with ID: {pluginId} removed successfully.");
    }
}