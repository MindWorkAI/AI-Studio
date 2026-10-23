using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks the migrations which add the hash of its text and its generation to every chunk.
/// </summary>
/// <remarks>
/// An index somebody built before keeps all of its chunks, and its full-text index keeps following
/// them: the migrations add columns with raw SQL rather than rebuilding the table, which would drop
/// the triggers behind the full-text index. A chunk stored before has no hash and is hashed from its
/// text when it is needed, and it is generation 0, so nothing has to be filled in.
/// </remarks>
[TestFixture]
public sealed class ChunkReuseMigrationTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";
    private const string PATH = "/tmp/test-data/report.md";
    private const string FINGERPRINT = "fingerprint";
    private const string OLD_TEXT = "The quarterly report was written before chunks kept a hash.";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    [Test]
    public async Task AnExistingIndexKeepsItsChunksAndItsFullTextSearch()
    {
        await using var store = await TemporaryIndexStore.CreateAsync("20261022000000_ChunkEndPageNumber");
        var parentFileId = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, PATH);
        var now = DateTimeOffset.UtcNow;

        //
        // Written the way the version before wrote it. The client would name the new columns, so
        // the chunk goes in with raw SQL:
        //
        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, new EmbeddingStateFile(parentFileId, PATH, "report.md", "report.md", "md", FINGERPRINT, 64, now, now, now, 1), TOKEN);
        await using (var context = store.CreateContext())
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO embedding_chunks (chunk_id, parent_file_id, page_number, end_page_number, chunk_index, chunk_text, embedded_at_utc) VALUES ({0}, {1}, NULL, NULL, 0, {2}, {3})",
                IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, FINGERPRINT, 0),
                parentFileId,
                OLD_TEXT,
                IndexStoreDateTimeOffset.ToUtcText(now));

        await store.MigrateAsync();

        var foundOldChunk = await store.Client.SearchChunksAsync(DATA_SOURCE_ID, "quarterly", 10, TOKEN);
        var storedChunks = await store.Client.GetStoredChunksAsync(DATA_SOURCE_ID, PATH, TOKEN);

        const string NEW_TEXT = "An appendix lists the budget.";
        await store.Client.UpsertChunksAsync(DATA_SOURCE_ID, [new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, FINGERPRINT, 1), parentFileId, null, null, 1, NEW_TEXT, now, ChunkTextHash.Of(NEW_TEXT), 1)], TOKEN);
        var foundNewChunk = await store.Client.SearchChunksAsync(DATA_SOURCE_ID, "budget", 10, TOKEN);
        List<(string? TextHash, int Generation)> storedColumns;
        await using (var context = store.CreateContext())
            storedColumns = (await context.EmbeddingChunks.OrderBy(chunk => chunk.ChunkIndex).Select(chunk => new { chunk.TextHash, chunk.Generation }).ToListAsync(TOKEN))
                .Select(chunk => (chunk.TextHash, chunk.Generation))
                .ToList();

        await store.Client.DeleteFileAsync(DATA_SOURCE_ID, PATH, TOKEN);
        var foundAfterDeleting = (await store.Client.SearchChunksAsync(DATA_SOURCE_ID, "quarterly", 10, TOKEN)).Concat(await store.Client.SearchChunksAsync(DATA_SOURCE_ID, "budget", 10, TOKEN));

        Assert.Multiple(() =>
        {
            Assert.That(foundOldChunk.Select(result => result.ChunkText), Is.EqualTo(new[] { OLD_TEXT }), "The chunk and its entry in the full-text index survive the migrations.");
            Assert.That(storedChunks, Is.EqualTo(new[] { new StoredChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, FINGERPRINT, 0), ChunkTextHash.Of(OLD_TEXT), 0) }), "A chunk stored before is hashed from its text, and it is generation 0.");
            Assert.That(foundNewChunk.Select(result => result.ChunkText), Is.EqualTo(new[] { NEW_TEXT }), "A chunk stored after the migrations reaches the full-text index.");
            Assert.That(storedColumns, Is.EqualTo(new (string?, int)[] { (null, 0), (ChunkTextHash.Of(NEW_TEXT), 1) }), "The old chunk is left as it was, and a new one keeps its hash and its generation.");
            Assert.That(foundAfterDeleting, Is.Empty, "Deleting the document removes its chunks from the full-text index.");
        });
    }
}