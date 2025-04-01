namespace AIStudio.Settings;

public interface IInternalDataSource : IDataSource
{
    /// <summary>
    /// The unique identifier of the embedding method used by this internal data source.
    /// </summary>
    public string EmbeddingId { get; init; }
}