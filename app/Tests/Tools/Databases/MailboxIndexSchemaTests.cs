using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks the tables the index keeps for mailboxes, above all what goes with what.
/// </summary>
/// <remarks>
/// What a delete takes with it is decided by the foreign keys, so a wrong one only shows once it
/// did its damage: a rebuild which tries a refused password again and locks the account, or a
/// removed folder which takes the mails along that were only moved out of it. These tests run
/// against a real SQLite file, migrated the way the app migrates its own, because only that
/// applies the cascades.
/// </remarks>
[TestFixture]
public sealed class MailboxIndexSchemaTests
{
    private const string DATA_SOURCE_ID = "0c3f9b52-7d4e-4a1b-9e6f-2b8c5d7a1e40";
    private const string MAIL_KEY = "mail:5d41402abc4b2a76b9719d911017c592";

    private static readonly string MAIL_ID = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, MAIL_KEY);

    [Test]
    public void TheModelSnapshotMatchesTheModel()
    {
        SQLitePCL.Batteries_V2.Init();
        using var context = new IndexStoreDbContext(IndexStoreDbContext.CreateOptions(Path.Combine(Path.GetTempPath(), "ai-studio-never-opened.sqlite3")));

        Assert.That(context.Database.HasPendingModelChanges(), Is.False, "EF Core refuses to migrate while the snapshot and the model disagree, and the app would start without its index.");
    }

    [Test]
    public async Task RebuildingAMailboxKeepsARefusedSignIn()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await AddIndexedMailAsync(store);

        await store.Client.DeleteDataSourceAsync(DATA_SOURCE_ID, CancellationToken.None);

        Assert.That(
            await CountRowsAsync(store),
            Is.EqualTo(new RowCounts(Messages: 0, Addresses: 0, Parts: 0, Folders: 0, Locations: 0, SyncStates: 0, AuthStates: 1)),
            "Everything of the index goes, but the refused sign-in stays. Otherwise the rebuild would try the refused password again.");
    }

    [Test]
    public async Task IndexingAMailAgainTakesItsMetadataAlong()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await AddIndexedMailAsync(store);

        // This is what indexing a document starts with:
        await store.Client.DeleteFileAsync(DATA_SOURCE_ID, MAIL_KEY, CancellationToken.None);

        Assert.That(
            await CountRowsAsync(store),
            Is.EqualTo(new RowCounts(Messages: 0, Addresses: 0, Parts: 0, Folders: 1, Locations: 0, SyncStates: 1, AuthStates: 1)),
            "The metadata of the mail goes with its document, which is why it is written after the last chunk. The folder and the sync state belong to the mailbox and stay.");
    }

    [Test]
    public async Task ARemovedFolderTakesItsLocationsButLeavesTheMail()
    {
        await using var store = await TemporaryIndexStore.CreateAsync();
        await AddIndexedMailAsync(store);

        await using (var context = store.CreateContext())
            await context.MailFolders.ExecuteDeleteAsync();

        Assert.That(
            await CountRowsAsync(store),
            Is.EqualTo(new RowCounts(Messages: 1, Addresses: 1, Parts: 1, Folders: 0, Locations: 0, SyncStates: 1, AuthStates: 1)),
            "The mail may have been moved to another folder, so whether it is gone is for the sync to decide, not for the folder.");
    }

    private static async Task AddIndexedMailAsync(TemporaryIndexStore store)
    {
        var token = CancellationToken.None;
        var now = DateTimeOffset.UtcNow;

        await store.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "MAILBOX", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", "source-hash", 3, token);
        await store.Client.UpsertFileAsync(DATA_SOURCE_ID, new EmbeddingStateFile(MAIL_ID, MAIL_KEY, "Quarterly report", MAIL_KEY, "mail", "fingerprint", 2048, now, now, now, 1), token);
        await store.Client.UpsertChunksAsync(DATA_SOURCE_ID, [new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(DATA_SOURCE_ID, "fingerprint", 0), MAIL_ID, null, 0, "From: Alice. Subject: Quarterly report.", now)], token);

        await using (var context = store.CreateContext())
        {
            var folder = new MailFolderEntity { DataSourceId = DATA_SOURCE_ID, Path = "INBOX", SpecialUse = "NONE", UidValidity = 1 };
            context.MailFolders.Add(folder);
            context.MailMessages.Add(new MailMessageEntity
            {
                ParentFileId = MAIL_ID,
                DataSourceId = DATA_SOURCE_ID,
                MessageId = "report@example.org",
                ReceivedAtUtc = now,
                Importance = "NORMAL",
                EncryptionKind = "NONE",
                MailHash = "mail-hash",
                FirstSeenUtc = now,
                Addresses = [new MailAddressEntity { Role = "FROM", Address = "alice@example.org", DisplayName = "Alice" }],
                Parts = [new MailPartEntity { Kind = "BODY", ContentType = "text/html", PartSize = 2048, Text = "The report.", TextState = "EXTRACTED" }],
                Locations = [new MailLocationEntity { Folder = folder, Uid = 42 }],
            });

            context.MailboxSyncStates.Add(new MailboxSyncStateEntity { DataSourceId = DATA_SOURCE_ID });
            context.MailboxAuthStates.Add(new MailboxAuthStateEntity { DataSourceId = DATA_SOURCE_ID, FailedAtUtc = now, FailureMessage = "Invalid credentials." });
            await context.SaveChangesAsync(token);
        }

        Assert.That(
            await CountRowsAsync(store),
            Is.EqualTo(new RowCounts(Messages: 1, Addresses: 1, Parts: 1, Folders: 1, Locations: 1, SyncStates: 1, AuthStates: 1)),
            "Every table has to hold a row first, or a test could not tell what a delete takes along.");
    }

    private static async Task<RowCounts> CountRowsAsync(TemporaryIndexStore store)
    {
        await using var context = store.CreateContext();
        return new RowCounts(
            await context.MailMessages.CountAsync(),
            await context.MailAddresses.CountAsync(),
            await context.MailParts.CountAsync(),
            await context.MailFolders.CountAsync(),
            await context.MailLocations.CountAsync(),
            await context.MailboxSyncStates.CountAsync(),
            await context.MailboxAuthStates.CountAsync());
    }

    // Compared as a whole, and printed as a whole when a test fails, so the properties are read through Equals and ToString only:
    // ReSharper disable NotAccessedPositionalProperty.Local
    private sealed record RowCounts(int Messages, int Addresses, int Parts, int Folders, int Locations, int SyncStates, int AuthStates);
    // ReSharper restore NotAccessedPositionalProperty.Local
}