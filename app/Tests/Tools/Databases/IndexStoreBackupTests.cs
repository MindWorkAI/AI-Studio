using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Data.Sqlite;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks the copy of the index which a backup takes along.
/// </summary>
/// <remarks>
/// After a restore, the copy is the index: what it lacks, AI Studio has to embed anew, at the cost of
/// the user's time and, with a cloud provider, money. The full-text search has to work from it right
/// away as well, since it lives in a table of its own.
/// </remarks>
[TestFixture]
public sealed class IndexStoreBackupTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";

    private const string FILE_PATH = "/tmp/test-data/report.md";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    [Test]
    public async Task TheCopyHoldsTheIndexAndItsFullTextSearch()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await StoreReportAsync(store);
        var targetDirectory = CreateTargetDirectory();

        try
        {
            var copyPath = await store.Client.BackupToDirectoryAsync(targetDirectory, TOKEN);
            var copy = new SqliteIndexStoreClientImplementation("SQLite", copyPath, targetDirectory, string.Empty);

            var manifest = await copy.GetManifestAsync(DATA_SOURCE_ID, TOKEN);
            var found = await copy.SearchChunksAsync(DATA_SOURCE_ID, "quarterly", 10, TOKEN);

            Assert.Multiple(() =>
            {
                Assert.That(Path.GetFileName(copyPath), Is.EqualTo("rag-index.sqlite3"), "A restore puts the copy where the index belongs, under the same name.");
                Assert.That(manifest.Files.Keys, Is.EqualTo(new[] { FILE_PATH }), "Without the fingerprint, the file would be embedded anew.");
                Assert.That(found.Select(result => result.ChunkText), Is.EqualTo(new[] { "The quarterly report is due." }));
            });
        }
        finally
        {
            RemoveTargetDirectory(targetDirectory);
        }
    }

    [Test]
    public async Task ACopyWrittenBeforeIsReplaced()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        var targetDirectory = CreateTargetDirectory();

        try
        {
            await store.Client.BackupToDirectoryAsync(targetDirectory, TOKEN);
            await StoreReportAsync(store);
            var copyPath = await store.Client.BackupToDirectoryAsync(targetDirectory, TOKEN);

            var copy = new SqliteIndexStoreClientImplementation("SQLite", copyPath, targetDirectory, string.Empty);
            Assert.That(await copy.GetTotalChunkCountAsync(TOKEN), Is.EqualTo(1));
        }
        finally
        {
            RemoveTargetDirectory(targetDirectory);
        }
    }

    [Test]
    public void WithoutAnIndexThereIsNothingToCopy()
    {
        var client = new NoIndexStoreClient("SQLite", "The native library could not be loaded.");

        Assert.That(async () => await client.BackupToDirectoryAsync(CreateTargetDirectory(), TOKEN), Throws.InvalidOperationException, "A backup without the index would take along vector stores which belong to nothing.");
    }

    private static async Task StoreReportAsync(TemporaryIndexStore store)
    {
        var parentFileId = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, FILE_PATH);
        var now = DateTimeOffset.UtcNow;

        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, new EmbeddingStateFile(parentFileId, FILE_PATH, "report.md", "report.md", "md", FILE_PATH, 64, now, now, now, 1), TOKEN);
        await store.Client.UpsertChunksAsync(DATA_SOURCE_ID, [new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, FILE_PATH, 0), parentFileId, null, null, 0, "The quarterly report is due.", now)], TOKEN);
    }

    private static string CreateTargetDirectory() => Path.Combine(Path.GetTempPath(), $"ai-studio-index-backup-{Guid.NewGuid():N}");

    private static void RemoveTargetDirectory(string targetDirectory)
    {
        // The client which opened the copy pools its connections, which would keep the file open on Windows:
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(targetDirectory))
            Directory.Delete(targetDirectory, true);
    }
}