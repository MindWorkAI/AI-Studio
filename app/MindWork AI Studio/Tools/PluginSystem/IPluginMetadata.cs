namespace AIStudio.Tools.PluginSystem;

public interface IPluginMetadata
{
    /// <summary>
    /// The icon of this plugin.
    /// </summary>
    public string IconSVG { get; }
    
    /// <summary>
    /// The type of this plugin.
    /// </summary>
    public PluginType Type { get; }
    
    /// <summary>
    /// The ID of this plugin.
    /// </summary>
    public Guid Id { get; }
    
    /// <summary>
    /// The name of this plugin.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// The description of this plugin.
    /// </summary>
    public string Description { get; }
    
    /// <summary>
    /// The version of this plugin.
    /// </summary>
    public PluginVersion Version { get; }

    /// <summary>
    /// The authors of this plugin.
    /// </summary>
    public string[] Authors { get; }
    
    /// <summary>
    /// The support contact for this plugin.
    /// </summary>
    public string SupportContact { get; }
    
    /// <summary>
    /// The source URL of this plugin.
    /// </summary>
    public string SourceURL { get; }
    
    /// <summary>
    /// The categories of this plugin.
    /// </summary>
    public PluginCategory[] Categories { get; }
    
    /// <summary>
    /// The target groups of this plugin.
    /// </summary>
    public PluginTargetGroup[] TargetGroups { get; }
    
    /// <summary>
    /// True, when the plugin is maintained.
    /// </summary>
    public bool IsMaintained { get; }
    
    /// <summary>
    /// The message that should be displayed when the plugin is deprecated.
    /// </summary>
    public string DeprecationMessage { get; }

    /// <summary>
    /// True, when the plugin is AI Studio internal.
    /// </summary>
    public bool IsInternal { get; }
}