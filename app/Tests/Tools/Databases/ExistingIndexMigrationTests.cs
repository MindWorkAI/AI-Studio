using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks the migration ChunkMetadata on an index which was built before it.
/// </summary>
/// <remarks>
/// It adds where a chunk is, the hash of its text and its generation to every chunk, and the hash
/// of its content and the generation it is searched by to every file. An index somebody built
/// before keeps all of its rows, and its full-text index keeps following them: the migration adds
/// columns with raw SQL rather than rebuilding a table, which would drop the triggers behind the
/// full-text index. A chunk stored before has no hash and is hashed from its text when it is
/// needed, it is generation 0 like its file, it names nothing but its start page, and its file has
/// no content hash until the file is read again. So nothing else has to be filled in.
/// </remarks>
[TestFixture]
public sealed class ExistingIndexMigrationTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";
    private const string PATH = "/tmp/test-data/report.md";
    private const string FINGERPRINT = "fingerprint";
    private const string OLD_TEXT = "The quarterly report was written before chunks kept a hash.";

    /// <summary>
    /// The last migration before ChunkMetadata, i.e., the schema of an index built by the version before.
    /// </summary>
    private const string SCHEMA_BEFORE = "20261018000000_MediaTranscriptKey";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    [Test]
    public async Task AnExistingIndexKeepsItsRowsAndItsFullTextSearch()
    {
        await using var store = await TemporaryIndexStore.CreateAsync(SCHEMA_BEFORE);
        var parentFileId = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, PATH);
        var oldChunkId = IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, FINGERPRINT, 0);
        var now = IndexStoreDateTimeOffset.ToUtcText(DateTimeOffset.UtcNow);

        //
        // Written the way the version before wrote it. The client would name the new columns, so
        // the rows of files and chunks go in with raw SQL:
        //
        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);
        await using (var context = store.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO embedded_files (parent_file_id, data_source_id, absolute_path, file_name, relative_path, file_type, fingerprint, file_size, creation_utc, last_write_utc, embedded_at_utc, chunk_count) VALUES ({0}, {1}, {2}, 'report.md', 'report.md', 'md', {3}, 64, {4}, {4}, {4}, 1)",
                parentFileId,
                DATA_SOURCE_ID,
                PATH,
                FINGERPRINT,
                now);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO embedding_chunks (chunk_id, parent_file_id, page_number, chunk_index, chunk_text, embedded_at_utc) VALUES ({0}, {1}, 4, 0, {2}, {3})",
                oldChunkId,
                parentFileId,
                OLD_TEXT,
                now);
        }

        await store.MigrateAsync();

        var manifest = await store.Client.GetManifestAsync(DATA_SOURCE_ID, TOKEN);
        var foundOldChunk = await store.Client.SearchChunksAsync(DATA_SOURCE_ID, "quarterly", 10, TOKEN);
        var storedChunks = await store.Client.GetStoredChunksAsync(DATA_SOURCE_ID, PATH, TOKEN);
        var oldLocations = await store.Client.GetChunkLocationsAsync(DATA_SOURCE_ID, [oldChunkId], TOKEN);

        //
        // Generation 0 as well, since the file is searched by it, and the search finds no other:
        //
        const string NEW_TEXT = "An appendix lists the budget.";
        await store.Client.UpsertChunksAsync(DATA_SOURCE_ID, [new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, FINGERPRINT, 1), parentFileId, null, null, 1, NEW_TEXT, DateTimeOffset.UtcNow, ChunkTextHash.Of(NEW_TEXT), StartLine: 12, EndLine: 14)], TOKEN);
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
            Assert.That(manifest.Files[PATH].Fingerprint, Is.EqualTo(FINGERPRINT), "The file survives the migration as it was.");
            Assert.That(manifest.Files[PATH].ContentHash, Is.Null, "A file indexed before has no content hash until it is read again.");
            Assert.That(manifest.Files[PATH].Generation, Is.Zero, "Its chunks are generation 0, and so is the file.");
            Assert.That(foundOldChunk.Select(result => result.ChunkText), Is.EqualTo(new[] { OLD_TEXT }), "The chunk and its entry in the full-text index survive the migration.");
            Assert.That(storedChunks, Is.EqualTo(new[] { new StoredChunk(oldChunkId, ChunkTextHash.Of(OLD_TEXT), 0) }), "A chunk stored before is hashed from its text, and it is generation 0.");
            Assert.That(oldLocations, Is.EquivalentTo(new Dictionary<string, ChunkLocation> { [oldChunkId] = new(PageNumber: 4) }), "A chunk stored before names its start page and nothing else.");
            Assert.That(foundNewChunk.Select(result => (result.ChunkText, result.StartLine, result.EndLine)), Is.EqualTo(new (string, int?, int?)[] { (NEW_TEXT, 12, 14) }), "A chunk stored after the migration reaches the full-text index, with where it is.");
            Assert.That(storedColumns, Is.EqualTo(new[] { (null, 0), (ChunkTextHash.Of(NEW_TEXT), 0) }), "The old chunk is left as it was, and a new one keeps its hash.");
            Assert.That(foundAfterDeleting, Is.Empty, "Deleting the document removes its chunks from the full-text index.");
        });
    }

    /// <remarks>
    /// Two generations of a document may hold the same position, which the unique index over
    /// parent and position used to refuse.
    /// </remarks>
    [Test]
    public async Task TwoGenerationsMayHoldTheSamePosition()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        var parentFileId = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, PATH);
        var now = DateTimeOffset.UtcNow;

        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, new EmbeddingStateFile(parentFileId, PATH, "report.md", "report.md", "md", FINGERPRINT, 64, now, now, now, 1), TOKEN);
        await store.Client.UpsertChunksAsync(DATA_SOURCE_ID,
        [
            new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, FINGERPRINT, 0), parentFileId, null, null, 0, "The stored version.", now),
            new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, IndexedDocumentIds.CreateChunkIdSource(DATA_SOURCE_ID, PATH, parentFileId, FINGERPRINT, 1), 0), parentFileId, null, null, 0, "The next version.", now, Generation: 1),
        ], TOKEN);

        var storedChunks = await store.Client.GetStoredChunksAsync(DATA_SOURCE_ID, PATH, TOKEN);

        Assert.That(storedChunks.Select(chunk => chunk.Generation), Is.EquivalentTo(new[] { 0, 1 }), "A new generation may hold the position of the stored one until it replaces it.");
    }
}