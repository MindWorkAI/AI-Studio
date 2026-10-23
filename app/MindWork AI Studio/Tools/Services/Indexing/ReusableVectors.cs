using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// The chunks a document had stored before it is read again, so that a chunk whose text did not
/// change keeps its vector.
/// </summary>
/// <remarks>
/// A vector depends on nothing but its text and the embedding signature, and all vectors of one
/// store share that signature: another one drops the store. So a chunk whose text is byte for byte
/// the text of a chunk the document had stored may take over that vector, instead of going to the
/// embedding provider again. That spares a file which was only saved again, or changed near its
/// end, almost every request.<br/><br/>
/// Only the id and the hash of each stored chunk are held in memory, about a hundred bytes per
/// chunk. The vectors stay in the vector store until the run is done with the document, and each
/// batch reads the few it can use, cf. TakeAsync. The new chunks belong to the next generation and
/// so have ids of their own, cf. NextGeneration: writing them never overwrites a vector a later
/// chunk may still need.
/// </remarks>
internal sealed class ReusableVectors
{
    private readonly Dictionary<string, string> chunkIdsByTextHash = new(StringComparer.Ordinal);

    private ReusableVectors(IReadOnlyList<StoredChunk> storedChunks)
    {
        foreach (var storedChunk in storedChunks)
            this.chunkIdsByTextHash.TryAdd(storedChunk.TextHash, storedChunk.ChunkId);

        //
        // A document stored for the first time, or again after a failure removed all of it,
        // starts at generation 0, which gives the ids documents got before generations existed:
        //
        this.NextGeneration = storedChunks.Count is 0 ? 0 : storedChunks.Max(storedChunk => storedChunk.Generation) + 1;
    }

    /// <summary>
    /// The generation of the chunks about to be written, cf. IndexedDocumentIds.CreateChunkIdSource.
    /// </summary>
    public int NextGeneration { get; }

    /// <summary>
    /// Reads which chunks a document has stored now, before anything of it is deleted.
    /// </summary>
    /// <param name="indexStore">The index store, which knows the chunks of the document and their texts.</param>
    /// <param name="dataSourceId">The data source the document belongs to.</param>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chunks of the document, which may be none.</returns>
    public static async Task<ReusableVectors> ReadAsync(IndexStoreClient indexStore, string dataSourceId, string documentKey, CancellationToken token) =>
        new(await indexStore.GetStoredChunksAsync(dataSourceId, documentKey, token));

    /// <summary>
    /// Finds the stored vector of every chunk of a batch whose text the document had before.
    /// </summary>
    /// <param name="vectorStore">The vector store, which still holds the vectors of the stored chunks.</param>
    /// <param name="collectionName">The vector store of the data source.</param>
    /// <param name="vectorSize">The size of the vectors in the store.</param>
    /// <param name="textHashes">The text hash of each chunk of the batch.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>For each chunk, in the same order, its stored vector, or null when it has to be embedded.</returns>
    public async Task<IReadOnlyList<float>?[]> TakeAsync(VectorStoreClient vectorStore, string collectionName, int vectorSize, IReadOnlyList<string> textHashes, CancellationToken token)
    {
        var vectors = new IReadOnlyList<float>?[textHashes.Count];
        if (this.chunkIdsByTextHash.Count == 0 || vectorSize <= 0)
            return vectors;

        var sourcePointIds = textHashes.Select(textHash => this.chunkIdsByTextHash.GetValueOrDefault(textHash)).ToArray();
        var pointIdsToRead = sourcePointIds.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (pointIdsToRead.Count == 0)
            return vectors;

        //
        // A vector of another size cannot come from this store as it is now, and the store would
        // refuse it next to the others:
        //
        var storedVectors = (await vectorStore.RetrieveVectorsAsync(collectionName, pointIdsToRead, token))
            .Where(storedVector => storedVector.Vector.Count == vectorSize)
            .ToDictionary(storedVector => storedVector.PointId, storedVector => storedVector.Vector, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < textHashes.Count; index++)
            if (sourcePointIds[index] is { } pointId && storedVectors.TryGetValue(pointId, out var storedVector))
                vectors[index] = storedVector;

        return vectors;
    }
}