// ReSharper disable InconsistentNaming

using AIStudio.Assistants.ERI;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// An external data source, accessed via an ERI server, cf. https://github.com/MindWorkAI/ERI.
/// </summary>
public readonly record struct DataSourceERI_V1 : IERIDataSource
{
    public DataSourceERI_V1()
    {
    }
    
    /// <inheritdoc />
    public uint Num { get; init; }

    /// <inheritdoc />
    public string Id { get; init; } = Guid.Empty.ToString();
    
    /// <inheritdoc />
    public string Name { get; init; } = string.Empty;
    
    /// <inheritdoc />
    public DataSourceType Type { get; init; } = DataSourceType.NONE;
    
    /// <inheritdoc />
    public string Hostname { get; init; } = string.Empty;
    
    /// <inheritdoc />
    public int Port { get; init; }

    /// <inheritdoc />
    public AuthMethod AuthMethod { get; init; } = AuthMethod.NONE;

    /// <inheritdoc />
    public string Username { get; init; } = string.Empty;

    /// <inheritdoc />
    public DataSourceSecurity SecurityPolicy { get; init; } = DataSourceSecurity.NOT_SPECIFIED;
    
    /// <inheritdoc />
    public ERIVersion Version { get; init; } = ERIVersion.V1;
}