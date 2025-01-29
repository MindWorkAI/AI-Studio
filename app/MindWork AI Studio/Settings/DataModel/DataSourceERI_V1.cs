// ReSharper disable InconsistentNaming

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
    
    /// <summary>
    /// The hostname of the ERI server.
    /// </summary>
    public string Hostname { get; init; } = string.Empty;
    
    /// <summary>
    /// The port of the ERI server.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// The authentication method to use.
    /// </summary>
    public AuthMethod AuthMethod { get; init; } = AuthMethod.NONE;

    /// <summary>
    /// The username to use for authentication, when the auth. method is USERNAME_PASSWORD.
    /// </summary>
    public string Username { get; init; } = string.Empty;
}