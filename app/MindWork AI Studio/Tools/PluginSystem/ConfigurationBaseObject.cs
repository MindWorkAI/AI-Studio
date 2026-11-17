namespace AIStudio.Tools.PluginSystem;

public abstract record ConfigurationBaseObject : IConfigurationObject
{
    #region Implementation of IConfigurationObject

    /// <inheritdoc />
    public abstract string Id { get; set; }

    /// <inheritdoc />
    public abstract uint Num { get; set; }

    /// <inheritdoc />
    public abstract string Name { get; set; }

    /// <inheritdoc />
    public abstract bool IsEnterpriseConfiguration { get; set; }

    /// <inheritdoc />
    public abstract Guid EnterpriseConfigurationPluginId { get; set; }

    #endregion
}