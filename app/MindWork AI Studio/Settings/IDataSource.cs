using System.Text.Json.Serialization;

using AIStudio.Chat;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.RAG;

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

    /// <summary>
    /// Which data security policy is applied to this data source?
    /// </summary>
    public DataSourceSecurity SecurityPolicy { get; init; }
    
    /// <summary>
    /// The maximum number of matches to return when retrieving data from the ERI server.
    /// </summary>
    public ushort MaxMatches { get; init; }
    
    /// <summary>
    /// Perform the data retrieval process.
    /// </summary>
    /// <param name="lastPrompt">The last prompt from the chat.</param>
    /// <param name="thread">The chat thread.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The retrieved data context.</returns>
    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IContent lastPrompt, ChatThread thread, CancellationToken token = default);
}