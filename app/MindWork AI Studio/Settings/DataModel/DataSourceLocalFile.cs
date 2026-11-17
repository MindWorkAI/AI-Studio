using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Represents one local file as a data source.
/// </summary>
public record struct DataSourceLocalFile: IInternalDataSource
{
    public DataSourceLocalFile()
    {
    }

    /// <inheritdoc />
    public uint Num { get; set; }

    /// <inheritdoc />
    public string Id { get; set; } = Guid.Empty.ToString();
    
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The description of the data source. What kind of data does it contain?
    /// What is the data source used for?
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <inheritdoc />
    public DataSourceType Type { get; set; } = DataSourceType.NONE;
    
    /// <inheritdoc />
    public string EmbeddingId { get; set; } = Guid.Empty.ToString();

    /// <inheritdoc />
    public int MaxChunkTokenLength { get; set; }

    /// <inheritdoc />
    public int ChunkOverlapTokenLength { get; set; } = DataSourceEmbeddingService.DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH;
    
    /// <inheritdoc />
    public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.UNKNOWN;

    /// <inheritdoc />
    public bool IsEnterpriseConfiguration { get; set; }

    /// <inheritdoc />
    public Guid EnterpriseConfigurationPluginId { get; set; } = Guid.Empty;
    
    /// <inheritdoc />
    public ushort MaxMatches { get; set; } = 10;

    /// <inheritdoc />
    public OutboundDataRestriction OutboundDataRestriction { get; set; } = DataSourceOutboundDataExtensions.DEFAULT_OUTBOUND_DATA_RESTRICTION;

    /// <inheritdoc />
    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IContent lastUserPrompt, ChatThread thread, CancellationToken token = default) =>
        Program.SERVICE_PROVIDER.GetRequiredService<DataSourceLocalRetrievalService>().RetrieveDataAsync(this, lastUserPrompt, thread, token);
    
    /// <inheritdoc />
    public Task<RetrievalPage> RetrieveDataAsync(string query, int page, ChatThread thread, CancellationToken token = default) =>
        Program.SERVICE_PROVIDER.GetRequiredService<DataSourceLocalRetrievalService>().RetrieveDataAsync(this, query, page, thread, token);
    
    /// <summary>
    /// The path to the file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
}