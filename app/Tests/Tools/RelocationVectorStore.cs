using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Keeps the identities and locations of existing vectors. New embeddings are forbidden: these
/// tests reconnect previously indexed files and must never contact an embedding provider.
/// </summary>
internal sealed class RelocationVectorStore() : VectorStoreClient("Relocation test vectors", string.Empty)
{
    public Dictionary<string, string> Locations { get; } = new(StringComparer.Ordinal);

    public List<string> RelocatedPaths { get; } = [];

    public TaskCompletionSource<object?> RelocationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool BlockNextRelocation { get; set; }

    public int CanceledRelocations { get; private set; }

    public int DeletedStores { get; private set; }

    public int DeletedDocuments { get; private set; }

    public int InsertedBatches { get; private set; }

    public override async Task RelocateEmbeddingFileAsync(string storeName, string parentFileId, string absolutePath, string relativePath, string fingerprint, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Assert.That(this.Locations.ContainsKey(parentFileId), Is.True, "Relocation must address an existing vector identity.");
        this.Locations[parentFileId] = absolutePath;
        this.RelocatedPaths.Add(absolutePath);
        if (!this.BlockNextRelocation)
            return;

        this.BlockNextRelocation = false;
        this.RelocationStarted.TrySetResult(null);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        catch (OperationCanceledException)
        {
            this.CanceledRelocations++;
            throw;
        }
    }

    public override Task<VectorStoreEnsureResult> EnsureVectorStoreExists(string storeName, string dataSourceName, int vectorSize, CancellationToken token) => Task.FromResult(new VectorStoreEnsureResult(false));

    public override Task<bool> VectorStoreExists(string storeName, CancellationToken token) => Task.FromResult(true);

    public override Task InsertEmbedding(string storeName, IReadOnlyList<VectorStoragePoint> points, CancellationToken token)
    {
        this.InsertedBatches++;
        throw new InvalidOperationException("Existing unchanged files must not be embedded again.");
    }

    public override Task DeleteEmbeddingByFile(string storeName, string filePath, string parentFileId, IReadOnlyCollection<string> keepPointIds, CancellationToken token)
    {
        this.DeletedDocuments++;
        foreach (var id in this.Locations.Where(entry => entry.Key == parentFileId || entry.Value == filePath).Select(entry => entry.Key).ToArray())
            this.Locations.Remove(id);
        return Task.CompletedTask;
    }

    public override Task DeleteVectorStore(string storeName, CancellationToken token)
    {
        this.DeletedStores++;
        this.Locations.Clear();
        return Task.CompletedTask;
    }

    public override Task OptimizeVectorStore(string storeName, CancellationToken token) => Task.CompletedTask;

    public override Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, CancellationToken token) => throw new NotSupportedException();

    public override Task<IReadOnlyList<VectorSearchResult>> SearchEmbeddingAsync(string storeName, IReadOnlyList<float> vector, int maxMatches, VectorSearchFilter filter, CancellationToken token) => throw new NotSupportedException();

    // Only the identities and locations are kept here, never a vector:
    public override Task<IReadOnlyList<StoredVector>> RetrieveVectorsAsync(string storeName, IReadOnlyList<string> pointIds, CancellationToken token) => Task.FromResult<IReadOnlyList<StoredVector>>([]);

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
