namespace AIStudio.Settings.DataModel;

/// <summary>
/// Represents a local directory as a data source.
/// </summary>
public readonly record struct DataSourceLocalDirectory : IInternalDataSource
{
    public DataSourceLocalDirectory()
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
    public string EmbeddingId { get; init; } = Guid.Empty.ToString();
    
    /// <summary>
    /// The path to the directory.
    /// </summary>
    public string Path { get; init; } = string.Empty;
}