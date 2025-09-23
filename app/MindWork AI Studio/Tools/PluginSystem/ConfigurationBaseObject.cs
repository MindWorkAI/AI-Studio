namespace AIStudio.Tools.PluginSystem;

public abstract record ConfigurationBaseObject : IConfigurationObject
{
    #region Implementation of IConfigurationObject

    /// <inheritdoc />
    public abstract string Id { get; init; }

    /// <inheritdoc />
    public abstract uint Num { get; init; }

    /// <inheritdoc />
    public abstract string Name { get; init; }

    /// <inheritdoc />
    public abstract bool IsEnterpriseConfiguration { get; init; }

    /// <inheritdoc />
    public abstract Guid EnterpriseConfigurationPluginId { get; init; }

    #endregion
}