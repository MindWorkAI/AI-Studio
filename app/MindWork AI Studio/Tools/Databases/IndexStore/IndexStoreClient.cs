using AIStudio.Tools.Services;

namespace AIStudio.Tools.Databases.IndexStore;

public abstract class IndexStoreClient(string name, string path) : DatabaseClient(name, path)
{
    public abstract Task<DataSourceEmbeddingManifest> GetManifestAsync(string dataSourceId, CancellationToken token);

    /// <summary>
    /// Reads what the index knows about a data source as a whole, without its files.
    /// </summary>
    /// <param name="dataSourceId">The data source to read.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The stored state, or null when the index holds nothing about this data source.</returns>
    public abstract Task<DataSourceIndexState?> GetDataSourceStateAsync(string dataSourceId, CancellationToken token);

    public abstract Task UpsertDataSourceAsync(
        string dataSourceId,
        string dataSourceType,
        string embeddingProviderId,
        string embeddingSignature,
        string sourceHash,
        int vectorSize,
        CancellationToken token);

    public abstract Task UpdateVectorSizeAsync(string dataSourceId, int vectorSize, CancellationToken token);

    public abstract Task UpdateDataSourceHashAsync(string dataSourceId, string sourceHash, CancellationToken token);

    public abstract Task UpsertFileAsync(string dataSourceId, EmbeddingStateFile file, CancellationToken token);

    public abstract Task DeleteFileAsync(string dataSourceId, string filePath, CancellationToken token);

    public abstract Task UpsertPermanentFailureAsync(string dataSourceId, PermanentIndexingFailure failure, CancellationToken token);

    public abstract Task DeletePermanentFailureAsync(string dataSourceId, string filePath, CancellationToken token);

    public abstract Task UpsertChunksAsync(string dataSourceId, IReadOnlyList<EmbeddingStateChunk> chunks, CancellationToken token);

    public abstract Task<IReadOnlyList<IndexStoreSearchResult>> SearchChunksAsync(string dataSourceId, string query, int maxMatches, CancellationToken token);

    public abstract Task DeleteDataSourceAsync(string dataSourceId, CancellationToken token);

    /// <summary>
    /// Counts the search chunks the index holds across all data sources.
    /// </summary>
    /// <remarks>
    /// One chunk is one vector: every chunk becomes exactly one point carrying the single named
    /// vector "embedding". The vector store reports its vector count from here, because counting
    /// the points in Qdrant Edge would have to load every shard first and would hold the global
    /// database mutex against ongoing inserts and searches while doing so.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The number of chunks, or null when the index cannot tell. Null and zero mean different things here.</returns>
    public abstract Task<long?> GetTotalChunkCountAsync(CancellationToken token);
}
