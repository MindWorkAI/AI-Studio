namespace AIStudio.Tools.PluginSystem;

public sealed class PluginMetadata(PluginBase plugin, string localPath) : IAvailablePlugin
{
    #region Implementation of IPluginMetadata

    /// <inheritdoc />
    public string IconSVG { get; } = plugin.IconSVG;
    
    /// <inheritdoc />
    public PluginType Type { get; } = plugin.Type;

    /// <inheritdoc />
    public Guid Id { get; } = plugin.Id;

    /// <inheritdoc />
    public string Name { get; } = plugin.Name;

    /// <inheritdoc />
    public string Description { get; } = plugin.Description;

    /// <inheritdoc />
    public PluginVersion Version { get; } = plugin.Version;

    /// <inheritdoc />
    public string[] Authors { get; } = plugin.Authors;

    /// <inheritdoc />
    public string SupportContact { get; } = plugin.SupportContact;

    /// <inheritdoc />
    public string SourceURL { get; } = plugin.SourceURL;

    /// <inheritdoc />
    public PluginCategory[] Categories { get; } = plugin.Categories;

    /// <inheritdoc />
    public PluginTargetGroup[] TargetGroups { get; } = plugin.TargetGroups;

    /// <inheritdoc />
    public bool IsMaintained { get; } = plugin.IsMaintained;

    /// <inheritdoc />
    public string DeprecationMessage { get; } = plugin.DeprecationMessage;

    /// <inheritdoc />
    public bool IsInternal { get; } = plugin.IsInternal;

    #endregion

    #region Implementation of IAvailablePlugin

    public string LocalPath { get; } = localPath;

    #endregion
}