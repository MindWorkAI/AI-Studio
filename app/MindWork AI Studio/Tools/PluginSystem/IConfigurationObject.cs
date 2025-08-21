namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a configuration object, such as a chat template or a LLM provider.
/// </summary>
public interface IConfigurationObject
{
    /// <summary>
    /// The unique ID of the configuration object.
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// The continuous number of the configuration object.
    /// </summary>
    public uint Num { get; }
    
    /// <summary>
    /// The name of the configuration object.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Is this configuration object an enterprise configuration?
    /// </summary>
    public bool IsEnterpriseConfiguration { get; }
    
    /// <summary>
    /// The ID of the enterprise configuration plugin.
    /// </summary>
    public Guid EnterpriseConfigurationPluginId { get; } 
}