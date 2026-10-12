using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings;

/// <summary>
/// What every configured data source has, whichever list of the settings it is stored in.
/// </summary>
/// <remarks>
/// Not every data source is an IDataSource. Those are the ones in DataSources, which classic RAG,
/// Semantic Search and the agents read. A data source kept in a list of its own is never handed to
/// them, and the compiler sees to that, because it implements only this interface or
/// IIndexedDataSource.
/// </remarks>
public interface IDataSourceBase : IConfigurationObject
{
    /// <summary>
    /// Which type of data source is this?
    /// </summary>
    public DataSourceType Type { get; init; }
}