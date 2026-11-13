using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks what the manifest of a data source tells about its documents.
/// </summary>
/// <remarks>
/// A document gets its row before its first chunk arrives, with no chunks at all, and its real
/// count once it is indexed. A run which is canceled in between, or an app which is closed
/// meanwhile, leaves such a row behind, perhaps together with some vectors. Read as an indexed
/// document, the file would never be indexed again; dropped, its leftovers would stay forever once
/// the file is gone or no longer selected.
/// </remarks>
[TestFixture]
public sealed class IndexStoreManifestTests
{
    private const string DATA_SOURCE_ID = "2b7c9e41-5d3a-4f86-a1e2-9c0d4b6f8a13";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    [Test]
    public async Task ARowWithoutChunksIsListedAsUnfinished()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);

        var indexedFile = Path.Combine(Path.GetTempPath(), "test-data", "report.pdf");
        var unfinishedFile = Path.Combine(Path.GetTempPath(), "test-data", "notes.md");
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, Row(indexedFile, 4), TOKEN);
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, Row(unfinishedFile, 0), TOKEN);

        var manifest = await store.Client.GetManifestAsync(DATA_SOURCE_ID, TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(manifest.Files.Keys, Is.EquivalentTo([indexedFile]), "A file whose indexing never came to an end has to be indexed again.");
            Assert.That(manifest.UnfinishedFiles, Is.EquivalentTo([unfinishedFile]), "Without this, nothing removes its leftovers once the file is gone or no longer selected.");
        }
    }

    [Test]
    public async Task TheManifestKnowsWhichTranscriptBelongsToAMediaFile()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);

        var recording = Path.Combine(Path.GetTempPath(), "test-data", "meeting.mp3");
        var document = Path.Combine(Path.GetTempPath(), "test-data", "report.pdf");
        var transcriptKey = new string('f', 64);
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, Row(recording, 2) with { TranscriptKey = transcriptKey }, TOKEN);
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, Row(document, 4), TOKEN);

        var manifest = await store.Client.GetManifestAsync(DATA_SOURCE_ID, TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(manifest.Files[recording].TranscriptKey, Is.EqualTo(transcriptKey), "Without the key, AI Studio cannot remove the transcript once the recording is gone.");
            Assert.That(manifest.Files[document].TranscriptKey, Is.Null);
        }
    }

    /// <remarks>
    /// When the fingerprint of a file changes, the stored hash of its content tells whether the
    /// file has to be read again at all.
    /// </remarks>
    [Test]
    public async Task TheManifestKnowsTheContentHashOfAFile()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);

        var hashedFile = Path.Combine(Path.GetTempPath(), "test-data", "report.pdf");
        var olderFile = Path.Combine(Path.GetTempPath(), "test-data", "notes.md");
        var contentHash = new string('a', 64);
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, Row(hashedFile, 4) with { ContentHash = contentHash }, TOKEN);
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, Row(olderFile, 2), TOKEN);

        var manifest = await store.Client.GetManifestAsync(DATA_SOURCE_ID, TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(manifest.Files[hashedFile].ContentHash, Is.EqualTo(contentHash));
            Assert.That(manifest.Files[olderFile].ContentHash, Is.Null, "A file indexed before the hash was kept is read again the next time it looks changed.");
        }
    }

    private static EmbeddingStateFile Row(string filePath, int chunkCount)
    {
        var now = DateTimeOffset.UtcNow;
        return new EmbeddingStateFile(IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, filePath), filePath, Path.GetFileName(filePath), Path.GetFileName(filePath), Path.GetExtension(filePath), "fingerprint", 1024, now, now, now, chunkCount);
    }
}