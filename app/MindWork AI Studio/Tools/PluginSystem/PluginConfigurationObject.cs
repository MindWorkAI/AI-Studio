using System.Linq.Expressions;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents metadata for a configuration object from a configuration plugin. These are
/// complex objects such as configured LLM providers, chat templates, etc.
/// </summary>
public sealed record PluginConfigurationObject
{
    private static readonly SettingsManager SETTINGS_MANAGER = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger<PluginConfigurationObject>();
    
    /// <summary>
    /// The id of the configuration plugin to which this configuration object belongs.
    /// </summary>
    public required Guid ConfigPluginId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// The id of the configuration object, e.g., the id of a chat template.
    /// </summary>
    public required Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The type of the configuration object.
    /// </summary>
    public required PluginConfigurationObjectType Type { get; init; } = PluginConfigurationObjectType.NONE;

    /// <summary>
    /// Cleans up configuration objects of a specified type that are no longer associated with any available plugin.
    /// </summary>
    /// <typeparam name="TClass">The type of configuration object to clean up.</typeparam>
    /// <param name="configObjectType">The type of configuration object to process.</param>
    /// <param name="configObjectSelection">A selection expression to retrieve the configuration objects from the main configuration.</param>
    /// <param name="availablePlugins">A list of currently available plugins.</param>
    /// <param name="configObjectList">A list of all existing configuration objects.</param>
    /// <returns>Returns true if the configuration was altered during cleanup; otherwise, false.</returns>
    public static bool CleanLeftOverConfigurationObjects<TClass>(
        PluginConfigurationObjectType configObjectType,
        Expression<Func<Data, List<TClass>>> configObjectSelection,
        IList<IAvailablePlugin> availablePlugins,
        IList<PluginConfigurationObject> configObjectList) where TClass : IConfigurationObject
    {
        var configuredObjects = configObjectSelection.Compile()(SETTINGS_MANAGER.ConfigurationData);
        var leftOverObjects = new List<TClass>();
        foreach (var configuredObject in configuredObjects)
        {
            if(!configuredObject.IsEnterpriseConfiguration)
                continue;

            var configObjectSourcePluginId = configuredObject.EnterpriseConfigurationPluginId;
            if(configObjectSourcePluginId == Guid.Empty)
                continue;
            
            var templateSourcePlugin = availablePlugins.FirstOrDefault(plugin => plugin.Id == configObjectSourcePluginId);
            if(templateSourcePlugin is null)
            {
                LOG.LogWarning($"The configured object '{configuredObject.Name}' (id={configuredObject.Id}) is based on a plugin that is not available anymore. Removing the chat template from the settings.");
                leftOverObjects.Add(configuredObject);
            }
            
            if(!configObjectList.Any(configObject =>
                   configObject.Type == configObjectType &&
                   configObject.ConfigPluginId == configObjectSourcePluginId &&
                   configObject.Id.ToString() == configuredObject.Id))
            {
                LOG.LogWarning($"The configured object '{configuredObject.Name}' (id={configuredObject.Id}) is not present in the configuration plugin anymore. Removing the chat template from the settings.");
                leftOverObjects.Add(configuredObject);
            }
        }
        
        // Remove collected items after enumeration to avoid modifying the collection during iteration:
        var wasConfigurationChanged = leftOverObjects.Count > 0;
        foreach (var item in leftOverObjects.Distinct())
            configuredObjects.Remove(item);
        
        return wasConfigurationChanged;
    }
}