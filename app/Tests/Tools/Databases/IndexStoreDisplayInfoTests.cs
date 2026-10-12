using AIStudio.Tools;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks the lines the information page shows about the local RAG index.
/// </summary>
/// <remarks>
/// Mails are documents of the index just like files, so they sit in the same table. A line about
/// files must not count them nonetheless.
/// </remarks>
[TestFixture]
public sealed class IndexStoreDisplayInfoTests
{
    private const string FOLDER_ID = "0b6e1f7c-3d2a-4e5f-9a8b-1c2d3e4f5a6b";
    private const string MAILBOX_ID = "7a8b9c0d-1e2f-4a3b-8c4d-5e6f7a8b9c0d";
    private const string EMBEDDING_ID = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    [Test]
    public async Task FilesAndMailsAreCountedApart()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await store.Client.UpsertDataSourceAsync(FOLDER_ID, "LOCAL_DIRECTORY", EMBEDDING_ID, "signature", "source-hash", 3, TOKEN);
        await store.Client.UpsertDataSourceAsync(MAILBOX_ID, "MAILBOX", EMBEDDING_ID, "signature", "source-hash", 3, TOKEN);

        await AddDocumentAsync(store, FOLDER_ID, "/tmp/test-data/minutes.md");
        await AddDocumentAsync(store, FOLDER_ID, "/tmp/test-data/budget.md");
        await AddDocumentAsync(store, MAILBOX_ID, MailKey('a'));
        await AddFailureAsync(store, FOLDER_ID, "/tmp/test-data/scan.pdf");
        await AddFailureAsync(store, MAILBOX_ID, MailKey('b'));

        var lines = await ReadDisplayInfoAsync(store);
        Assert.Multiple(() =>
        {
            Assert.That(lines["Indexed files"], Is.EqualTo("2"));
            Assert.That(lines["Permanently skipped files"], Is.EqualTo("1"));
            Assert.That(lines["Indexed mails"], Is.EqualTo("1"));
            Assert.That(lines["Permanently skipped mails"], Is.EqualTo("1"));
        });
    }

    [Test]
    public async Task WithoutMailsTheLinesAboutMailsAreLeftOut()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await store.Client.UpsertDataSourceAsync(FOLDER_ID, "LOCAL_DIRECTORY", EMBEDDING_ID, "signature", "source-hash", 3, TOKEN);
        await AddDocumentAsync(store, FOLDER_ID, "/tmp/test-data/minutes.md");

        var lines = await ReadDisplayInfoAsync(store);
        Assert.Multiple(() =>
        {
            Assert.That(lines["Indexed files"], Is.EqualTo("1"));
            Assert.That(lines, Does.Not.ContainKey("Indexed mails"));
            Assert.That(lines, Does.Not.ContainKey("Permanently skipped mails"));
        });
    }

    private static string MailKey(char filler) => MailContentKey.PREFIX + new string(filler, 64);

    private static Task AddDocumentAsync(TemporaryIndexStore store, string dataSourceId, string key)
    {
        var now = DateTimeOffset.UtcNow;
        return store.Client.UpsertFileAsync(dataSourceId, new EmbeddingStateFile(IndexedDocumentIds.CreateParentId(dataSourceId, key), key, "name", "name", "md", "fingerprint", 64, now, now, now, 1), TOKEN);
    }

    private static Task AddFailureAsync(TemporaryIndexStore store, string dataSourceId, string key) => store.Client.UpsertPermanentFailureAsync(
        dataSourceId,
        new PermanentIndexingFailure(IndexedDocumentIds.CreateParentId(dataSourceId, key), key, "fingerprint", FileExtractionErrorCode.NO_CONTENT, "No text could be read.", DateTimeOffset.UtcNow),
        TOKEN);

    private static async Task<Dictionary<string, string>> ReadDisplayInfoAsync(TemporaryIndexStore store)
    {
        var lines = new Dictionary<string, string>();
        await foreach (var (label, value) in store.Client.GetDisplayInfo())
            lines[label] = value;

        return lines;
    }
}