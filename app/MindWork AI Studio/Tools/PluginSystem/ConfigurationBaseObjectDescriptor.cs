using System.Linq.Expressions;

using AIStudio.Settings.DataModel;

using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Describes a type of configuration object which derives from ConfigurationBaseObject.
/// </summary>
/// <param name="type">The type of the configuration objects.</param>
/// <param name="luaTableName">The Lua table in which a configuration plugin defines these objects.</param>
/// <param name="selection">Where the settings keep these objects.</param>
/// <param name="nextNumSelection">The number which the next of these objects gets.</param>
/// <param name="parser">Reads one of these objects from its Lua table.</param>
/// <param name="secretStoreType">Where the API keys of these objects are kept, or null for objects without one.</param>
/// <typeparam name="TClass">The class of the configuration objects.</typeparam>
public sealed class ConfigurationBaseObjectDescriptor<TClass>(PluginConfigurationObjectType type, string luaTableName, Expression<Func<Data, List<TClass>>> selection, Expression<Func<Data, uint>> nextNumSelection, ConfigurationObjectParser parser, SecretStoreType? secretStoreType = null) : PluginConfigurationObjectDescriptor where TClass : ConfigurationBaseObject
{
    /// <inheritdoc />
    public override PluginConfigurationObjectType Type => type;

    /// <inheritdoc />
    public override string LuaTableName => luaTableName;

    /// <summary>
    /// Where the settings keep these objects.
    /// </summary>
    public Expression<Func<Data, List<TClass>>> Selection => selection;

    /// <summary>
    /// The number which the next of these objects gets.
    /// </summary>
    public Expression<Func<Data, uint>> NextNumSelection => nextNumSelection;

    /// <summary>
    /// Reads one of these objects from its Lua table.
    /// </summary>
    public ConfigurationObjectParser Parser => parser;

    /// <inheritdoc />
    public override bool TryParse(LuaTable mainTable, Guid configPluginId, List<PluginConfigurationObject> configObjects, bool dryRun, string pluginPath) => PluginConfigurationObject.TryParse(this, mainTable, configPluginId, configObjects, dryRun, pluginPath);

    /// <inheritdoc />
    public override Task<bool> CleanLeftOverAsync(IList<IAvailablePlugin> availablePlugins, IReadOnlySet<Guid> deployedEnterpriseConfigPluginIds, IReadOnlySet<Guid> notStartedConfigPluginIds, IList<PluginConfigurationObject> configObjectList) => PluginConfigurationObject.CleanLeftOverConfigurationObjects(type, selection, availablePlugins, deployedEnterpriseConfigPluginIds, notStartedConfigPluginIds, configObjectList, secretStoreType);

    /// <inheritdoc />
    public override int RemovePluginObjects(Data settings) => selection.Compile()(settings).RemoveAll(configObject => configObject.IsEnterpriseConfiguration);
}