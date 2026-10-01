using AIStudio.Provider;

namespace AIStudio.Settings;

/// <summary>
/// A data source whose content AI Studio embeds and indexes itself.
/// </summary>
/// <remarks>
/// The embedding signature, the chunking and the embedding provider are worked out from these
/// members alone. That is why the indexer serves every data source implementing this interface,
/// whether it is stored in DataSources or not.
/// </remarks>
public interface IIndexedDataSource : IDataSourceBase
{
    /// <summary>
    /// Which provider confidence level is required by this data source?
    /// </summary>
    public ConfidenceLevel ConfidenceLevel { get; init; }

    /// <summary>
    /// The unique identifier of the embedding method used by this data source.
    /// </summary>
    public string EmbeddingId { get; init; }

    /// <summary>
    /// Optional maximum number of tokens per embedding chunk for this data source.
    /// A value of 0 means the embedding provider's setting is used.
    /// </summary>
    public int MaxChunkTokenLength { get; init; }

    /// <summary>
    /// Optional number of tokens to overlap between consecutive chunks.
    /// </summary>
    public int ChunkOverlapTokenLength { get; init; }
}