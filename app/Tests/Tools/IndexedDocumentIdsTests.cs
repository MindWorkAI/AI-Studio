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
}