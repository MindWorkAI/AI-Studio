using AIStudio.Tools;
using AIStudio.Tools.Databases.IndexStore;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks the row the index keeps for a data source as a whole.
/// </summary>
/// <remarks>
/// Whether a data source has an index at all is read from this row alone. A data source can end up
/// with no document indexed but with documents known to fail, e.g., a folder of scans without a
/// text layer, and those failures hang on this row.
/// </remarks>
[TestFixture]
public sealed class IndexStoreDataSourceStateTests
{
    private const string DATA_SOURCE_ID = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11";

    [Test]
    public async Task ADataSourceWithOnlyFailedDocumentsKeepsItsRowUntilItIsDeleted()
    {
        var token = CancellationToken.None;
        await using var store = await TemporaryIndexStore.CreateAsync();

        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "LOCAL_DIRECTORY", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", "source-hash", 3, token);
        await store.Client.UpsertPermanentFailureAsync(
            DATA_SOURCE_ID,
            new PermanentIndexingFailure("d06c4351-2e57-2941-aa4c-d7a814bfa418", "/tmp/test-data/scan.pdf", "fingerprint", FileExtractionErrorCode.NO_CONTENT, "No text could be read.", DateTimeOffset.UtcNow),
            token);

        Assert.That(
            await store.Client.GetDataSourceStateAsync(DATA_SOURCE_ID, token),
            Is.Not.Null,
            "No document made it into the index, but the failures did. Without the row nothing would remember them, and every run would read the scans again.");

        await store.Client.DeleteDataSourceAsync(DATA_SOURCE_ID, token);

        var stateAfterDeletion = await store.Client.GetDataSourceStateAsync(DATA_SOURCE_ID, token);
        await using var context = store.CreateContext();
        var failuresAfterDeletion = await context.PermanentIndexingFailures.CountAsync(token);

        Assert.Multiple(() =>
        {
            Assert.That(stateAfterDeletion, Is.Null);
            Assert.That(failuresAfterDeletion, Is.Zero, "The failures go with the data source.");
        });
    }
}