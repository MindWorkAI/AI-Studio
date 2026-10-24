using AIStudio.Tools.Databases.ProvenanceStore;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks the store of what chats received besides their messages.
/// </summary>
/// <remarks>
/// A chat refers to its entries by id only, so an entry has to come back exactly as it was stored,
/// and has to go with its chat, but never with another one.
/// </remarks>
[TestFixture]
public sealed class ProvenanceStoreTests
{
    private static readonly CancellationToken TOKEN = CancellationToken.None;

    [Test]
    public void TheModelSnapshotMatchesTheModel()
    {
        SQLitePCL.Batteries_V2.Init();
        using var context = new ProvenanceStoreDbContext(ProvenanceStoreDbContext.CreateOptions(Path.Combine(Path.GetTempPath(), "ai-studio-never-opened-provenance.sqlite3")));

        Assert.That(context.Database.HasPendingModelChanges(), Is.False, "EF Core refuses to migrate while the snapshot and the model disagree, and the app would start without its provenance store.");
    }

    [Test]
    public async Task AnEntryComesBackAsItWasStored()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var id = await store.Client.StoreAsync(chatId, ProvenanceKind.ERI_PASSAGE, "The budget grew by 4 %.\nÄnderungen folgen.", ["Before the passage.", "After the passage."], TOKEN);
        var entries = await store.Client.GetEntriesAsync([id!.Value], TOKEN);

        Assert.That(entries, Contains.Key(id.Value));
        var entry = entries[id.Value];
        Assert.Multiple(() =>
        {
            Assert.That(entry.Id, Is.EqualTo(id.Value));
            Assert.That(entry.Kind, Is.EqualTo(ProvenanceKind.ERI_PASSAGE));
            Assert.That(entry.Content, Is.EqualTo("The budget grew by 4 %.\nÄnderungen folgen."));
            Assert.That(entry.SurroundingContent, Is.EqualTo(new[] { "Before the passage.", "After the passage." }));
            Assert.That(entry.CreatedAtUtc, Is.GreaterThan(before));
        });
    }

    [Test]
    public async Task AnEntryWithoutSurroundingContentStoresNone()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();

        var withNull = await store.Client.StoreAsync(Guid.NewGuid(), ProvenanceKind.ATTACHMENT, "file content", null, TOKEN);
        var withEmpty = await store.Client.StoreAsync(Guid.NewGuid(), ProvenanceKind.WEB_PAGE, "page content", [], TOKEN);
        var entries = await store.Client.GetEntriesAsync([withNull!.Value, withEmpty!.Value], TOKEN);

        await using var context = store.CreateContext();
        var rowsWithSurroundingContent = await context.Entries.CountAsync(entry => entry.SurroundingContent != null);
        Assert.Multiple(() =>
        {
            Assert.That(entries[withNull.Value].SurroundingContent, Is.Empty);
            Assert.That(entries[withEmpty.Value].SurroundingContent, Is.Empty);
            Assert.That(rowsWithSurroundingContent, Is.Zero, "Only an ERI passage brings content around it, so every other row keeps the column empty.");
        });
    }

    [Test]
    public async Task EveryStoredItemGetsARowOfItsOwn()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatId = Guid.NewGuid();

        var first = await store.Client.StoreAsync(chatId, ProvenanceKind.WEB_PAGE, "the same page", null, TOKEN);
        var second = await store.Client.StoreAsync(chatId, ProvenanceKind.WEB_PAGE, "the same page", null, TOKEN);

        Assert.That(second, Is.Not.EqualTo(first), "One row per item: two calls which read the same page are two items.");
    }

    [Test]
    public async Task AnUnknownIdIsMissingFromTheAnswer()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var known = await store.Client.StoreAsync(Guid.NewGuid(), ProvenanceKind.TOOL_RESULT, "{\"result_count\": 0}", null, TOKEN);

        var entries = await store.Client.GetEntriesAsync([known!.Value, Guid.NewGuid()], TOKEN);

        Assert.That(entries.Keys, Is.EquivalentTo(new[] { known.Value }));
    }

    [Test]
    public async Task NoIdsReadNothing()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();

        Assert.That(await store.Client.GetEntriesAsync([], TOKEN), Is.Empty);
    }

    [Test]
    public async Task ACopyKeepsTheIdsAndOutlivesItsOrigin()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var assistantChat = Guid.NewGuid();
        var chat = Guid.NewGuid();
        var passage = await store.Client.StoreAsync(assistantChat, ProvenanceKind.DATA_SOURCE_PASSAGE, "a passage", null, TOKEN);
        var page = await store.Client.StoreAsync(assistantChat, ProvenanceKind.WEB_PAGE, "a page", null, TOKEN);

        await store.Client.CopyChatAsync(assistantChat, chat, TOKEN);
        await store.Client.DeleteChatAsync(assistantChat, TOKEN);
        var entries = await store.Client.GetEntriesAsync([passage!.Value, page!.Value], TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(entries[passage.Value].Content, Is.EqualTo("a passage"));
            Assert.That(entries[page.Value].Content, Is.EqualTo("a page"));
        });
    }

    [Test]
    public async Task CopyingTwiceLeavesOneCopy()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        await store.Client.StoreAsync(source, ProvenanceKind.MAIL, "a mail", null, TOKEN);

        await store.Client.CopyChatAsync(source, target, TOKEN);
        await store.Client.CopyChatAsync(source, target, TOKEN);

        await using var context = store.CreateContext();
        Assert.That(await context.Entries.CountAsync(entry => entry.ChatId == target.ToString("D")), Is.EqualTo(1));
    }

    [Test]
    public async Task DeletingAChatLeavesTheOthersAlone()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var deletedChat = Guid.NewGuid();
        var keptChat = Guid.NewGuid();
        var deleted = await store.Client.StoreAsync(deletedChat, ProvenanceKind.ATTACHMENT, "deleted", null, TOKEN);
        var kept = await store.Client.StoreAsync(keptChat, ProvenanceKind.ATTACHMENT, "kept", null, TOKEN);

        await store.Client.DeleteChatAsync(deletedChat, TOKEN);
        var entries = await store.Client.GetEntriesAsync([deleted!.Value, kept!.Value], TOKEN);
        var chatIds = await store.Client.GetChatIdsAsync(TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { kept.Value }));
            Assert.That(chatIds, Is.EquivalentTo(new[] { keptChat }));
        });
    }

    [Test]
    public async Task DeletingTheUnreferencedEntriesOfAChatKeepsTheReferencedOnes()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chat = Guid.NewGuid();
        var otherChat = Guid.NewGuid();
        var referenced = await store.Client.StoreAsync(chat, ProvenanceKind.DATA_SOURCE_PASSAGE, "still in the chat", null, TOKEN);
        var removed = await store.Client.StoreAsync(chat, ProvenanceKind.DATA_SOURCE_PASSAGE, "of a deleted message", null, TOKEN);
        var otherChatsEntry = await store.Client.StoreAsync(otherChat, ProvenanceKind.DATA_SOURCE_PASSAGE, "of another chat", null, TOKEN);

        await store.Client.DeleteChatEntriesExceptAsync(chat, [referenced!.Value], TOKEN);
        var entries = await store.Client.GetEntriesAsync([referenced.Value, removed!.Value, otherChatsEntry!.Value], TOKEN);

        Assert.That(entries.Keys, Is.EquivalentTo(new[] { referenced.Value, otherChatsEntry.Value }));
    }

    [Test]
    public async Task TheChatsWithEntriesAreListedOnce()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var firstChat = Guid.NewGuid();
        var secondChat = Guid.NewGuid();
        await store.Client.StoreAsync(firstChat, ProvenanceKind.ATTACHMENT, "one", null, TOKEN);
        await store.Client.StoreAsync(firstChat, ProvenanceKind.TOOL_RESULT, "two", null, TOKEN);
        await store.Client.StoreAsync(secondChat, ProvenanceKind.MAIL, "three", null, TOKEN);

        Assert.That(await store.Client.GetChatIdsAsync(TOKEN), Is.EquivalentTo(new[] { firstChat, secondChat }));
    }

    [Test]
    public async Task TheBackupCopyHoldsEveryEntry()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var id = await store.Client.StoreAsync(Guid.NewGuid(), ProvenanceKind.ERI_PASSAGE, "a passage", ["around it"], TOKEN);
        var copyDirectory = CreateCopyDirectory();

        try
        {
            var copyPath = await store.Client.BackupToDirectoryAsync(copyDirectory, TOKEN);
            var copy = new SqliteProvenanceStoreClientImplementation("SQLite", copyPath, copyDirectory, string.Empty);
            var entries = await copy.GetEntriesAsync([id!.Value], TOKEN);

            Assert.Multiple(() =>
            {
                Assert.That(Path.GetFileName(copyPath), Is.EqualTo(SqliteProvenanceStoreClientImplementation.DATABASE_FILENAME), "A restore puts the copy where the store belongs, under the same name.");
                Assert.That(entries[id.Value].Content, Is.EqualTo("a passage"));
                Assert.That(entries[id.Value].SurroundingContent, Is.EqualTo(new[] { "around it" }));
            });
        }
        finally
        {
            RemoveCopyDirectory(copyDirectory);
        }
    }

    [Test]
    public async Task AChatOfABackupArrivesWithItsEntriesAndTheirIds()
    {
        await using var backup = await TemporaryProvenanceStore.CreateAsync();
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatInBackup = Guid.NewGuid();
        var otherChatInBackup = Guid.NewGuid();
        var importedChat = Guid.NewGuid();
        var passage = await backup.Client.StoreAsync(chatInBackup, ProvenanceKind.ERI_PASSAGE, "a passage", ["around it"], TOKEN);
        var page = await backup.Client.StoreAsync(chatInBackup, ProvenanceKind.WEB_PAGE, "a page", null, TOKEN);
        var otherChatsMail = await backup.Client.StoreAsync(otherChatInBackup, ProvenanceKind.MAIL, "a mail of another chat", null, TOKEN);
        var copyDirectory = CreateCopyDirectory();

        try
        {
            var copyPath = await backup.Client.BackupToDirectoryAsync(copyDirectory, TOKEN);

            var imported = await store.Client.ImportChatAsync(copyPath, chatInBackup, importedChat, TOKEN);
            var importedAgain = await store.Client.ImportChatAsync(copyPath, chatInBackup, importedChat, TOKEN);
            var entries = await store.Client.GetEntriesAsync([passage!.Value, page!.Value, otherChatsMail!.Value], TOKEN);
            var chatIds = await store.Client.GetChatIdsAsync(TOKEN);

            Assert.Multiple(() =>
            {
                Assert.That(imported, Is.EqualTo(2));
                Assert.That(importedAgain, Is.Zero, "Entries the chat holds already stay as they are.");
                Assert.That(entries.Keys, Is.EquivalentTo(new[] { passage.Value, page.Value }), "The chat refers to its entries by their ids, also under its new id. Another chat's entries stay behind.");
                Assert.That(entries[passage.Value].Kind, Is.EqualTo(ProvenanceKind.ERI_PASSAGE));
                Assert.That(entries[passage.Value].SurroundingContent, Is.EqualTo(new[] { "around it" }));
                Assert.That(chatIds, Is.EquivalentTo(new[] { importedChat }));
            });
        }
        finally
        {
            RemoveCopyDirectory(copyDirectory);
        }
    }

    [Test]
    public void TheStandInMakesNoCopy()
    {
        var client = new NoProvenanceStoreClient("SQLite", "not available");

        Assert.That(async () => await client.BackupToDirectoryAsync(Path.GetTempPath(), TOKEN), Throws.InvalidOperationException, "An empty copy would pass for the real one.");
    }

    [Test]
    public async Task TheStandInKeepsNothing()
    {
        var client = new NoProvenanceStoreClient("SQLite", "not available");

        var id = await client.StoreAsync(Guid.NewGuid(), ProvenanceKind.ATTACHMENT, "content", null, TOKEN);

        Assert.That(id, Is.Null, "No id tells the caller to keep the content in the chat itself.");
    }

    [Test]
    public async Task TheInformationPageCountsEntriesChatsAndKinds()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chat = Guid.NewGuid();
        await store.Client.StoreAsync(chat, ProvenanceKind.ATTACHMENT, "one", null, TOKEN);
        await store.Client.StoreAsync(chat, ProvenanceKind.WEB_PAGE, "two", null, TOKEN);
        await store.Client.StoreAsync(chat, ProvenanceKind.WEB_PAGE, "three", null, TOKEN);
        await store.Client.StoreAsync(Guid.NewGuid(), ProvenanceKind.MAIL, "four", null, TOKEN);

        var lines = await ReadDisplayInfoAsync(store);

        Assert.Multiple(() =>
        {
            Assert.That(lines["Stored contents in all chats"], Is.EqualTo("4"));
            Assert.That(lines["Chats with stored contents"], Is.EqualTo("2"));
            Assert.That(lines["Attached files"], Is.EqualTo("1"));
            Assert.That(lines["Web pages"], Is.EqualTo("2"));
            Assert.That(lines["Mails"], Is.EqualTo("1"));
            Assert.That(lines, Does.Not.ContainKey("Tool results"), "A kind no chat received would only say zero.");
            Assert.That(lines["Journal mode"], Is.EqualTo("WAL"));
            Assert.That(lines["Schema version"], Does.StartWith("20261024000000_InitialProvenanceStore"));
        });
    }

    [Test]
    public async Task AnEmptyStoreShowsZeroAndNoKinds()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();

        var lines = await ReadDisplayInfoAsync(store);

        Assert.Multiple(() =>
        {
            Assert.That(lines["Stored contents in all chats"], Is.EqualTo("0"));
            Assert.That(lines["Chats with stored contents"], Is.EqualTo("0"));
            Assert.That(lines, Does.Not.ContainKey("Attached files"));
        });
    }

    private static string CreateCopyDirectory() => Path.Combine(Path.GetTempPath(), $"ai-studio-provenance-copy-{Guid.NewGuid():N}");

    private static void RemoveCopyDirectory(string copyDirectory)
    {
        // The copy is read without a pool, but the clients of the stores keep theirs:
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(copyDirectory))
            Directory.Delete(copyDirectory, true);
    }

    private static async Task<Dictionary<string, string>> ReadDisplayInfoAsync(TemporaryProvenanceStore store)
    {
        var lines = new Dictionary<string, string>();
        await foreach (var (label, value) in store.Client.GetDisplayInfo())
            lines[label] = value;

        return lines;
    }
}