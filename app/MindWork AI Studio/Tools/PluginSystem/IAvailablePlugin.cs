namespace AIStudio.Tools.PluginSystem;

public interface IAvailablePlugin : IPluginMetadata
{
    public string LocalPath { get; }
    
    public bool IsManagedByConfigServer { get; }

    public Guid? ManagedConfigurationId { get; }

    /// <summary>
    /// The priority of a configuration plugin. Zero for every other plugin type.
    /// </summary>
    /// <remarks>
    /// Configuration plugins with a higher priority start later and therefore win when two of them
    /// manage the same setting or define the same configuration object. The priority only orders
    /// plugins of the same origin: a local configuration plugin never starts before one which an
    /// organization deployed, no matter which priority it declares.
    /// </remarks>
    public int ConfigurationPriority { get; }
}