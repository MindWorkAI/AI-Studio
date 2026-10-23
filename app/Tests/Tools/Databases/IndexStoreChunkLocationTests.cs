using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks that the index keeps where each chunk is: its sheet and rows, its lines, and the attachment of a mail it is from.
/// </summary>
/// <remarks>
/// The vector store keeps only the pages of a chunk, so the index store is the one place these are
/// kept, and every passage the user is shown takes them from there.
/// </remarks>
[TestFixture]
public sealed class IndexStoreChunkLocationTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";
    private const string PATH = "/tmp/test-data/budget.xlsx";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    [Test]
    public async Task AChunkKeepsWhereItIs()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);

        var parentFileId = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, PATH);
        var now = DateTimeOffset.UtcNow;
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, new EmbeddingStateFile(parentFileId, PATH, "budget.xlsx", "budget.xlsx", "xlsx", "fingerprint", 64, now, now, now, 3), TOKEN);
        await store.Client.UpsertChunksAsync(DATA_SOURCE_ID,
        [
            new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, "fingerprint", 0), parentFileId, null, null, 0, "Rent and food.", now, SheetName: "Budget", StartRow: 3, EndRow: 7),
            new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, "fingerprint", 1), parentFileId, 2, 3, 1, "Costs fell.", now, StartLine: 12, EndLine: 40, AttachmentPosition: 1),
            new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, "fingerprint", 2), parentFileId, null, null, 2, "Says nothing about where it is.", now),
        ], TOKEN);

        List<(string? SheetName, int? StartRow, int? EndRow, int? StartLine, int? EndLine, int? AttachmentPosition)> stored;
        await using (var context = store.CreateContext())
            stored = (await context.EmbeddingChunks.OrderBy(chunk => chunk.ChunkIndex).ToListAsync(TOKEN))
                .Select(chunk => (chunk.SheetName, chunk.StartRow, chunk.EndRow, chunk.StartLine, chunk.EndLine, chunk.AttachmentPosition))
                .ToList();

        Assert.That(stored, Is.EqualTo(new (string?, int?, int?, int?, int?, int?)[]
        {
            ("Budget", 3, 7, null, null, null),
            (null, null, null, 12, 40, 1),
            (null, null, null, null, null, null),
        }), "A chunk without a location keeps none, which reads as unknown.");
    }
}