namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents metadata for a configuration object from a configuration plugin. These are
/// complex objects such as configured LLM providers, chat templates, etc.
/// </summary>
public sealed record PluginConfigurationObject
{
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
}