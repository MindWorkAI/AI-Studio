using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Represents one local file as a data source.
/// </summary>
public readonly record struct DataSourceLocalFile : IInternalDataSource
{
    public DataSourceLocalFile()
    {
    }

    /// <inheritdoc />
    public uint Num { get; init; }

    /// <inheritdoc />
    public string Id { get; init; } = Guid.Empty.ToString();
    
    /// <inheritdoc />
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The description of the data source. What kind of data does it contain?
    /// What is the data source used for?
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <inheritdoc />
    public DataSourceType Type { get; init; } = DataSourceType.NONE;
    
    /// <inheritdoc />
    public string EmbeddingId { get; init; } = Guid.Empty.ToString();

    /// <inheritdoc />
    public int MaxChunkTokenLength { get; init; }

    /// <inheritdoc />
    public int ChunkOverlapTokenLength { get; init; } = DataSourceEmbeddingService.DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH;
    
    /// <inheritdoc />
    public ConfidenceLevel ConfidenceLevel { get; init; } = ConfidenceLevel.UNKNOWN;

    /// <inheritdoc />
    public bool IsEnterpriseConfiguration { get; init; }

    /// <inheritdoc />
    public Guid EnterpriseConfigurationPluginId { get; init; } = Guid.Empty;
    
    /// <inheritdoc />
    public ushort MaxMatches { get; init; } = 10;
    
    /// <inheritdoc />
    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IContent lastUserPrompt, ChatThread thread, CancellationToken token = default) =>
        Program.SERVICE_PROVIDER.GetRequiredService<DataSourceLocalRetrievalService>().RetrieveDataAsync(this, lastUserPrompt, thread, token);
    
    /// <summary>
    /// The path to the file.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;
}