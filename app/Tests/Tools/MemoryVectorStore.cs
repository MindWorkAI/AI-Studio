using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Keeps the points of one data source in memory, vectors included, for tests which index documents.
/// </summary>
internal sealed class MemoryVectorStore() : VectorStoreClient("Test vectors in memory", string.Empty)
{
    public Dictionary<string, VectorStoragePoint> Points { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The points each call of RetrieveVectorsAsync asked for, in the order of the calls.
    /// </summary>
    public List<IReadOnlyList<string>> RetrieveRequests { get; } = [];

    /// <summary>
    /// Called with the points of every InsertEmbedding before they are kept, to look at the other store at that moment.
    /// </summary>
    public Func<IReadOnlyList<VectorStoragePoint>, Task>? BeforeInsert { get; set; }

    public override Task<VectorStoreEnsureResult> EnsureVectorStoreExists(string storeName, string dataSourceName, int vectorSize, CancellationToken token) => Task.FromResult(new VectorStoreEnsureResult(this.Points.Count == 0));

    public override Task<bool> VectorStoreExists(string storeName, CancellationToken token) => Task.FromResult(true);

    public override async Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token)
    {
        if (this.BeforeInsert is not null)
            await this.BeforeInsert(points);

        foreach (var point in points)
            this.Points[point.PointId] = point;
    }

    public override Task<IReadOnlyList<StoredVector>> RetrieveVectorsAsync(string storeName, IReadOnlyList<string> pointIds, CancellationToken token)
    {
        this.RetrieveRequests.Add(pointIds.ToList());
        return Task.FromResult<IReadOnlyList<StoredVector>>(pointIds
            .Where(this.Points.ContainsKey)
            .Select(pointId => new StoredVector(pointId, this.Points[pointId].Vector))
            .ToList());
    }

    public override Task DeleteEmbeddingByFile(string storeName, string filePath, string parentFileId, IReadOnlyCollection<string> keepPointIds, CancellationToken token)
    {
        var deletedPointIds = this.Points.Values
            .Where(point => (point.FilePath == filePath || point.ParentFileId == parentFileId) && !keepPointIds.Contains(point.PointId))
            .Select(point => point.PointId)
            .ToList();

        foreach (var pointId in deletedPointIds)
            this.Points.Remove(pointId);

        return Task.CompletedTask;
    }

    public override Task DeleteVectorStore(string storeName, CancellationToken token)
    {
        this.Points.Clear();
        return Task.CompletedTask;
    }

    public override Task OptimizeVectorStore(string storeName, CancellationToken token) => Task.CompletedTask;

    public override Task RelocateEmbeddingFileAsync(string storeName, string parentFileId, string absolutePath, string relativePath, string fingerprint, CancellationToken token)
    {
        foreach (var point in this.Points.Values.Where(point => point.ParentFileId == parentFileId).ToList())
            this.Points[point.PointId] = point with { AbsolutePath = absolutePath, RelativePath = relativePath, Fingerprint = fingerprint };

        return Task.CompletedTask;
    }

    public override Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, IReadOnlyList<HiddenChunks> hiddenChunks, CancellationToken token) => throw new NotSupportedException();

    public override Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, VectorSearchFilter filter, IReadOnlyList<HiddenChunks> hiddenChunks, CancellationToken token) => throw new NotSupportedException();

    public override Task<VectorStoreBackupPreparation> PrepareBackupAsync(CancellationToken token) => throw new NotSupportedException();

    public override Task<long> CopyStoreForBackupAsync(string backupId, string storeName, CancellationToken token) => throw new NotSupportedException();

    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        await Task.CompletedTask;
        yield break;
    }

    public override void Dispose()
    {
    }
}