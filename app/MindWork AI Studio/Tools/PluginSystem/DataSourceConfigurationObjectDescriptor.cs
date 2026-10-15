using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Describes the data sources which configuration plugins provide.
/// </summary>
/// <remarks>
/// The settings keep every data source in a list of IDataSource, and a data source of a configuration
/// plugin is a record struct rather than a ConfigurationBaseObject. That is why data sources are read
/// in a way of their own.
/// </remarks>
public sealed class DataSourceConfigurationObjectDescriptor : PluginConfigurationObjectDescriptor
{
    /// <inheritdoc />
    public override PluginConfigurationObjectType Type => PluginConfigurationObjectType.DATA_SOURCE;

    /// <inheritdoc />
    public override bool TryParse(LuaTable mainTable, Guid configPluginId, List<PluginConfigurationObject> configObjects, bool dryRun, string pluginPath) => PluginConfigurationObject.TryParseDataSources(mainTable, configPluginId, configObjects, dryRun);

    /// <inheritdoc />
    /// <remarks>
    /// The secret of a data source is not necessarily an API key, e.g., it is a password, so it is
    /// deleted as a secret.
    /// </remarks>
    public override Task<bool> CleanLeftOverAsync(IList<IAvailablePlugin> availablePlugins, IReadOnlySet<Guid> deployedEnterpriseConfigPluginIds, IReadOnlySet<Guid> notStartedConfigPluginIds, IList<PluginConfigurationObject> configObjectList) => PluginConfigurationObject.CleanLeftOverConfigurationObjects(this.Type, x => x.DataSources, availablePlugins, deployedEnterpriseConfigPluginIds, notStartedConfigPluginIds, configObjectList, SecretStoreType.DATA_SOURCE, deleteSecret: true);
}