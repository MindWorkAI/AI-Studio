using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks the full-text search over the chunks of a data source of files.
/// </summary>
/// <remarks>
/// Its results are cut into pages by asking for more of them each time, cf. RetrievalPaging. So
/// chunks of the same score have to come in the same order with every limit, or a page would show
/// a chunk again or skip one.
/// </remarks>
[TestFixture]
public sealed class IndexStoreFullTextSearchTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    [Test]
    public async Task TheBestChunkComesFirstAndTiesKeepAFixedOrder()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);

        var best = await StoreFileAsync(store, "/tmp/test-data/best.md", "report report report", "nothing to see here");
        var tieOne = await StoreFileAsync(store, "/tmp/test-data/tie-one.md", "a report among other words");
        var tieTwo = await StoreFileAsync(store, "/tmp/test-data/tie-two.md", "a report among other words");
        var ties = new[] { tieOne, tieTwo }.Order(StringComparer.Ordinal).ToArray();

        var all = await store.Client.SearchChunksAsync(DATA_SOURCE_ID, "report", 10, TOKEN);
        var firstTwo = await store.Client.SearchChunksAsync(DATA_SOURCE_ID, "report", 2, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(all.Select(result => (result.ParentFileId, result.ChunkIndex)), Is.EqualTo(new[] { (best, 0), (ties[0], 0), (ties[1], 0) }), "The chunk without the word is not found, and the tie falls by document.");
            Assert.That(firstTwo.Select(result => result.ParentFileId), Is.EqualTo(new[] { best, ties[0] }), "A smaller limit cuts the same order short.");
        });
    }

    private static async Task<string> StoreFileAsync(TemporaryIndexStore store, string path, params string[] chunkTexts)
    {
        var parentFileId = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, path);
        var now = DateTimeOffset.UtcNow;

        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, new EmbeddingStateFile(parentFileId, path, Path.GetFileName(path), Path.GetFileName(path), "md", path, 64, now, now, now, chunkTexts.Length), TOKEN);
        await store.Client.UpsertChunksAsync(
            DATA_SOURCE_ID,
            chunkTexts.Select((text, index) => new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, path, index), parentFileId, null, index, text, now)).ToList(),
            TOKEN);

        return parentFileId;
    }
}