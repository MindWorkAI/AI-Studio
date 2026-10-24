namespace AIStudio.Tools.Databases.VectorStore;

public sealed record VectorSearchResult(
    string PointId,
    double Score,
    string DataSourceId,
    string DataSourceType,
    string ChunkId,
    string ParentFileId,
    string FilePath,
    string AbsolutePath,
    string FileName,
    string RelativePath,
    string FileType,
    int? PageNumber,
    int? EndPageNumber,
    int ChunkIndex,
    string Text,
    string Fingerprint,
    string CreationUtc,
    string LastWriteUtc,
    string EmbeddedAtUtc)
{
    /// <summary>
    /// Where the chunk is, as the index store keeps it, or null until the search completed the hit from there.
    /// </summary>
    /// <remarks>
    /// The vector store keeps only the pages of a chunk, cf. LocalIndexSearchService.SearchVectorsAsync.
    /// </remarks>
    public ChunkLocation? StoredLocation { get; init; }

    /// <summary>
    /// Where the chunk is: what the index store keeps, or the pages alone when it could not say.
    /// </summary>
    public ChunkLocation Location => this.StoredLocation ?? new ChunkLocation(this.PageNumber, this.EndPageNumber);
}
