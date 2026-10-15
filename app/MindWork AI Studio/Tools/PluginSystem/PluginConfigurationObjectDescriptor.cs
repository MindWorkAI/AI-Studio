using AIStudio.Settings.DataModel;

using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Describes one type of configuration object which configuration plugins provide, such as LLM
/// providers or chat templates.
/// </summary>
/// <remarks>
/// Whatever treats every type of configuration object alike goes through these descriptors, so that
/// none of these places can forget a type: reading the objects from a configuration plugin, removing
/// those a plugin no longer defines, and leaving all of them out of a backup. A new type of
/// configuration object needs a descriptor in PluginConfigurationObjectDescriptors, and every such place
/// handles it.
/// </remarks>
public abstract class PluginConfigurationObjectDescriptor
{
    /// <summary>
    /// The type of the configuration objects.
    /// </summary>
    public abstract PluginConfigurationObjectType Type { get; }

    /// <summary>
    /// Reads the configuration objects of this type which a configuration plugin defines, and stores
    /// them in the settings unless the plugin is only checked.
    /// </summary>
    /// <param name="mainTable">The main table of the configuration plugin.</param>
    /// <param name="configPluginId">The configuration plugin.</param>
    /// <param name="configObjects">The list to which every object read is added.</param>
    /// <param name="dryRun">Whether the plugin is only checked, e.g., before an import. Nothing is stored then.</param>
    /// <param name="pluginPath">The directory of the plugin, which paths in the objects are relative to.</param>
    /// <returns>True when the plugin defines a table for this type.</returns>
    public abstract bool TryParse(LuaTable mainTable, Guid configPluginId, List<PluginConfigurationObject> configObjects, bool dryRun, string pluginPath);

    /// <summary>
    /// Removes the configuration objects of this type which belong to a configuration plugin that is
    /// gone or no longer defines them, along with their secrets and tokenizers.
    /// </summary>
    /// <param name="availablePlugins">The plugins AI Studio found.</param>
    /// <param name="deployedEnterpriseConfigPluginIds">The configuration plugins an organization deployed, including those which could not be loaded.</param>
    /// <param name="notStartedConfigPluginIds">The configuration plugins which were loaded, but did not start.</param>
    /// <param name="configObjectList">The configuration objects all running configuration plugins define.</param>
    /// <returns>True when the settings changed.</returns>
    public abstract Task<bool> CleanLeftOverAsync(IList<IAvailablePlugin> availablePlugins, IReadOnlySet<Guid> deployedEnterpriseConfigPluginIds, IReadOnlySet<Guid> notStartedConfigPluginIds, IList<PluginConfigurationObject> configObjectList);

    /// <summary>
    /// Removes the configuration objects of this type which a configuration plugin manages.
    /// </summary>
    /// <param name="settings">The settings to change, e.g., a copy for a backup. Never the settings AI Studio runs with.</param>
    /// <returns>How many objects were removed.</returns>
    public abstract int RemovePluginObjects(Data settings);
}