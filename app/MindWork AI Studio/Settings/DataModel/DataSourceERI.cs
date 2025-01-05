namespace AIStudio.Settings.DataModel;

/// <summary>
/// An external data source, accessed via an ERI server, cf. https://github.com/MindWorkAI/ERI.
/// </summary>
public readonly record struct DataSourceERI : IDataSource
{
    public DataSourceERI()
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
}