using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

/// <summary>
/// The common interface for all data sources.
/// </summary>
public interface IDataSource
{
    /// <summary>
    /// The number of the data source.
    /// </summary>
    public uint Num { get; init; }

    /// <summary>
    /// The unique identifier of the data source.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// The name of the data source.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Which type of data source is this?
    /// </summary>
    public DataSourceType Type { get; init; }
}