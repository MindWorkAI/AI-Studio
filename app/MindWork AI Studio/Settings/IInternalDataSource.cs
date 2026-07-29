namespace AIStudio.Settings;

public interface IInternalDataSource : IDataSource
{
    /// <summary>
    /// The unique identifier of the embedding method used by this internal data source.
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