using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools.Databases;

public sealed class IndexStoreRelocationTests
{
    [Test]
    public async Task RelocationPreservesChunksAndTranscriptAndUpdatesSearchLocations()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        var sourceId = Guid.NewGuid().ToString();
        var oldPath = Path.Combine(Path.GetTempPath(), "original", "notes.md");
        var newPath = Path.Combine(Path.GetTempPath(), "moved", "renamed.md");
        var parentId = IndexedDocumentIds.CreateParentId(sourceId, oldPath);
        var chunkId = IndexedDocumentIds.CreateChunkId(sourceId, "old-fingerprint", 0);
        var now = DateTimeOffset.UtcNow;
        var token = CancellationToken.None;
        await store.Client.UpsertDataSourceAsync(sourceId, "LOCAL_DIRECTORY", "provider", "signature", "old-hash", 3, token);
        await store.Client.UpsertFileAsync(sourceId, new EmbeddingStateFile(parentId, oldPath, "notes.md", "notes.md", "md", "old-fingerprint", 100, now, now, now, 1) { TranscriptKey = "transcript" }, token);
        await store.Client.UpsertChunksAsync(sourceId, [new EmbeddingStateChunk(chunkId, parentId, 2, 0, "report about relocation", now)], token);

        // Repeating the metadata write after an interrupted relocation must keep the same chunks.
        await store.Client.RelocateFileAsync(sourceId, parentId, newPath, "renamed.md", "new-fingerprint", token);
        await store.Client.RelocateFileAsync(sourceId, parentId, newPath, "renamed.md", "new-fingerprint", token);
        var manifest = await store.Client.GetManifestAsync(sourceId, token);
        var hits = await store.Client.SearchChunksAsync(sourceId, "relocation", 10, token);

        Assert.Multiple(() =>
        {
            Assert.That(manifest.Files.Keys, Is.EquivalentTo(new[] { newPath }));
            Assert.That(manifest.Files[newPath].Fingerprint, Is.EqualTo("new-fingerprint"));
            Assert.That(manifest.Files[newPath].ParentFileId, Is.EqualTo(parentId));
            Assert.That(manifest.Files[newPath].RelativePath, Is.EqualTo("renamed.md"));
            Assert.That(manifest.Files[newPath].TranscriptKey, Is.EqualTo("transcript"));
            Assert.That(hits, Has.Count.EqualTo(1));
            Assert.That(hits[0].ChunkId, Is.EqualTo(chunkId));
            Assert.That(hits[0].AbsolutePath, Is.EqualTo(newPath));
            Assert.That(hits[0].FileName, Is.EqualTo("renamed.md"));
            Assert.That(hits[0].PageNumber, Is.EqualTo(2));
            Assert.That(hits[0].ChunkText, Is.EqualTo("report about relocation"));
        });

        await store.Client.DeleteFileAsync(sourceId, newPath, token);
        Assert.That(await store.Client.SearchChunksAsync(sourceId, "relocation", 10, token), Is.Empty, "Ordinary removal must still find a relocated document's chunks.");
    }
}
