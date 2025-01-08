using System.Text.Json.Serialization;

using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

/// <summary>
/// The common interface for all data sources.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type_discriminator")]
[JsonDerivedType(typeof(DataSourceLocalDirectory), nameof(DataSourceType.LOCAL_DIRECTORY))]
[JsonDerivedType(typeof(DataSourceLocalFile), nameof(DataSourceType.LOCAL_FILE))]
[JsonDerivedType(typeof(DataSourceERI_V1), nameof(DataSourceType.ERI_V1))]
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