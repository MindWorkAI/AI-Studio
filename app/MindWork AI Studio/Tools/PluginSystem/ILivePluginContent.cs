namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents complex content from a configuration plugin that is read live from
/// running plugins and is not persisted to the settings data model.
/// </summary>
public interface ILivePluginContent
{
    /// <summary>
    /// The stable ID of the live plugin content.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The ID of the enterprise configuration plugin that provides this content.
    /// </summary>
    public Guid EnterpriseConfigurationPluginId { get; }
}