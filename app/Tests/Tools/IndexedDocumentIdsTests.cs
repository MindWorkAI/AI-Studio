using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Pins the ids under which documents and their chunks are stored.
/// </summary>
/// <remarks>
/// Every stored chunk and every stored document row carries an id made with these formats, in the
/// vector store as well as in the index store. Changing one would leave everything already stored
/// under ids nothing produces any more, so these tests make that a decision somebody takes rather
/// than something which happens while code is moved around.
/// </remarks>
[TestFixture]
public sealed class IndexedDocumentIdsTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";

    [Test]
    public void TheChunkIdOfAKnownChunkIsPinned()
    {
        Assert.Multiple(() =>
        {
            Assert.That(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, "3F9A0C", 0), Is.EqualTo("7c9d2822-f9d1-a94e-8d83-30cea4f0856f"));
            Assert.That(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, "3F9A0C", 1), Is.EqualTo("5830c96c-7c3c-2f47-8d54-6eadf04cddf3"), "Each chunk of a document has an id of its own.");
        });
    }

    [Test]
    public void TheParentIdOfAKnownDocumentIsPinned()
    {
        Assert.That(IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, "/tmp/test-data/report.pdf"), Is.EqualTo("d06c4351-2e57-2941-aa4c-d7a814bfa418"));
    }

    [Test]
    public void ADocumentStoredUnderTheIdOfItsKeyKeepsItsChunkIds()
    {
        const string KEY = "/tmp/test-data/report.pdf";
        var source = IndexedDocumentIds.CreateChunkIdSource(DATA_SOURCE_ID, KEY, IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, KEY), "3F9A0C");

        Assert.That(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, source, 0), Is.EqualTo("7c9d2822-f9d1-a94e-8d83-30cea4f0856f"), "Files and mails which never moved must keep the ids they are stored under.");
    }

    /// <remarks>
    /// A document indexed again gets new chunk ids even when its fingerprint stays, as a mail's does,
    /// so the new chunks never overwrite the old ones while those are still needed.
    /// </remarks>
    [Test]
    public void TheChunkIdOfALaterGenerationIsPinned()
    {
        const string KEY = "/tmp/test-data/report.pdf";
        var parentId = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, KEY);

        Assert.Multiple(() =>
        {
            Assert.That(IndexedDocumentIds.CreateChunkIdSource(DATA_SOURCE_ID, KEY, parentId, "3F9A0C", 0), Is.EqualTo("3F9A0C"), "The first generation gives the ids documents got before generations existed.");
            Assert.That(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, IndexedDocumentIds.CreateChunkIdSource(DATA_SOURCE_ID, KEY, parentId, "3F9A0C", 1), 0), Is.EqualTo("10e9f029-bc45-a048-9db1-e5ac74777ddb"));
        });
    }

    [Test]
    public void TheChunkIdOfAMovedDocumentIsPinned()
    {
        var source = IndexedDocumentIds.CreateChunkIdSource(DATA_SOURCE_ID, "/tmp/moved/report.pdf", "0b5c2f7e-3d41-4a8e-9f60-2c7d1e8a4b93", "3F9A0C");

        Assert.That(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, source, 0), Is.EqualTo("aa597d9b-ede8-5449-80d0-f209cde0ecc0"), "A file at the old place with the same fingerprint must not get the chunk ids of the moved one.");
    }
}