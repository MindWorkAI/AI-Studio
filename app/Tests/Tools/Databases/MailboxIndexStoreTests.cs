using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks how the index keeps track of mails, their folders and the places they lie in.
/// </summary>
/// <remarks>
/// Embedding a mail is the expensive part, with a cloud provider a paid one. So what matters most
/// here is that nothing forces a mail to be embedded again which is only somewhere else now: a
/// mail moved to another folder, a folder whose UIDs the server renumbered, a mail which shows up
/// in a later run than the one it disappeared in.
/// </remarks>
[TestFixture]
public sealed class MailboxIndexStoreTests
{
    private const string DATA_SOURCE_ID = "0c3f9b52-7d4e-4a1b-9e6f-2b8c5d7a1e40";
    private const string KEY_A = "mail:5d41402abc4b2a76b9719d911017c592";
    private const string KEY_B = "mail:7d793037a0760186574b0282f2f435e7";
    private const string INBOX = "INBOX";
    private const string ARCHIVE = "Archive/2026";

    private static readonly DateTimeOffset FIRST_SEEN = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken TOKEN = CancellationToken.None;

    private TemporaryIndexStore store = null!;

    private IndexStoreClient Client => this.store.Client;

    [SetUp]
    public async Task CreateMailboxAsync()
    {
        this.store = await TemporaryIndexStore.CreateAsync();
        await this.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "MAILBOX", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);
        await this.Client.UpsertMailFolderAsync(DATA_SOURCE_ID, Folder(INBOX, 1), TOKEN);
        await this.Client.UpsertMailFolderAsync(DATA_SOURCE_ID, Folder(ARCHIVE, 1), TOKEN);
    }

    [TearDown]
    public async Task DeleteMailboxAsync() => await this.store.DisposeAsync();

    [Test]
    public async Task AMailComesBackAsItWasStored()
    {
        var mail = Mail(await this.StoreDocumentAsync(KEY_A), At(INBOX, 7, isSeen: true), At(ARCHIVE, 3));
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, mail, TOKEN);

        var stored = await this.Client.GetMailAsync(DATA_SOURCE_ID, mail.MailId, TOKEN);

        Assert.That(stored, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stored! with { ReferenceMessageIds = mail.ReferenceMessageIds, Addresses = mail.Addresses, Parts = mail.Parts, Locations = mail.Locations }, Is.EqualTo(mail), "The dates come back in UTC, but have to name the same moments.");
            Assert.That(stored.ReferenceMessageIds, Is.EqualTo(mail.ReferenceMessageIds));
            Assert.That(stored.Addresses, Is.EqualTo(mail.Addresses));
            Assert.That(stored.Parts, Is.EqualTo(mail.Parts));
            Assert.That(stored.Locations, Is.EquivalentTo(mail.Locations));
        });
    }

    [Test]
    public async Task AFolderComesBackAsItWasStoredAndKeepsItsMails()
    {
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(await this.StoreDocumentAsync(KEY_A), At(INBOX, 7)), TOKEN);
        var synced = new MailFolderRecord(INBOX, MailFolderSpecialUse.NONE, 1, 812, 4711, 811, 12, FIRST_SEEN.AddHours(1));

        await this.Client.UpsertMailFolderAsync(DATA_SOURCE_ID, synced, TOKEN);

        var folders = await this.Client.GetMailFoldersAsync(DATA_SOURCE_ID, TOKEN);
        var inboxLocations = await this.Client.GetMailLocationsAsync(DATA_SOURCE_ID, INBOX, TOKEN);
        Assert.Multiple(() =>
        {
            Assert.That(folders, Is.EqualTo(new[] { Folder(ARCHIVE, 1), synced }), "Ordered by path.");
            Assert.That(inboxLocations.Keys, Is.EquivalentTo(new long[] { 7 }), "The UIDVALIDITY did not change, so the UIDs stay valid.");
        });
    }

    [Test]
    public async Task StoringAMailAgainReplacesItsDetailsButNotWhenItWasFirstSeen()
    {
        var mailId = await this.StoreDocumentAsync(KEY_A);
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(mailId, At(INBOX, 7)), TOKEN);
        var again = Mail(mailId, At(ARCHIVE, 3)) with
        {
            FirstSeenUtc = FIRST_SEEN.AddDays(10),
            Addresses = [new MailAddressRecord(MailAddressRole.FROM, "alice@example.org", "Alice")],
            Parts = [new MailPartRecord(MailPartKind.BODY, string.Empty, "text/plain", 12, "The report.", MailPartTextState.EXTRACTED)],
        };

        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, again, TOKEN);

        var stored = await this.Client.GetMailAsync(DATA_SOURCE_ID, mailId, TOKEN);
        var inboxLocations = await this.Client.GetMailLocationsAsync(DATA_SOURCE_ID, INBOX, TOKEN);
        Assert.Multiple(() =>
        {
            Assert.That(stored!.FirstSeenUtc, Is.EqualTo(FIRST_SEEN), "The mail was found before, and when it was first seen stays the earlier moment.");
            Assert.That(stored.Addresses, Is.EqualTo(again.Addresses));
            Assert.That(stored.Parts, Is.EqualTo(again.Parts));
            Assert.That(stored.Locations, Is.EqualTo(again.Locations));
            Assert.That(inboxLocations, Is.Empty, "The places are replaced as well, not added to.");
        });
    }

    [Test]
    public async Task AMovedMailIsLinkedAnewInsteadOfBeingEmbeddedAgain()
    {
        var mailId = await this.StoreDocumentAsync(KEY_A);
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(mailId, At(INBOX, 7)), TOKEN);

        await this.Client.RemoveMailLocationsAsync(DATA_SOURCE_ID, INBOX, [7], TOKEN);
        var orphanedAfterRemoval = await this.Client.GetOrphanedMailsAsync(DATA_SOURCE_ID, DateTimeOffset.UtcNow.AddMinutes(1), TOKEN);

        var linked = await this.Client.AddMailLocationAsync(DATA_SOURCE_ID, mailId, At(ARCHIVE, 3, isSeen: true), TOKEN);
        var orphanedAfterLinking = await this.Client.GetOrphanedMailsAsync(DATA_SOURCE_ID, DateTimeOffset.UtcNow.AddMinutes(1), TOKEN);
        var archiveLocations = await this.Client.GetMailLocationsAsync(DATA_SOURCE_ID, ARCHIVE, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(orphanedAfterRemoval, Is.EqualTo(new[] { KEY_A }), "Without any place left, the mail is an orphan.");
            Assert.That(linked, Is.True);
            Assert.That(orphanedAfterLinking, Is.Empty, "Found in another folder, the mail is no orphan any more and keeps its embedding.");
            Assert.That(archiveLocations, Is.EqualTo(new Dictionary<long, MailFlags> { [3] = new(true, false, false) }));
        });
    }

    [Test]
    public async Task AMailTheIndexDoesNotHoldCannotBeLinked()
    {
        var linked = await this.Client.AddMailLocationAsync(DATA_SOURCE_ID, IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, KEY_A), At(INBOX, 7), TOKEN);

        Assert.That(linked, Is.False, "The caller has to index such a mail instead.");
    }

    [Test]
    public async Task AnOrphanIsOnlyDueInTheRunAfterTheOneWhichOrphanedIt()
    {
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(await this.StoreDocumentAsync(KEY_A), At(INBOX, 7)), TOKEN);

        var runStart = DateTimeOffset.UtcNow;
        await this.Client.RemoveMailLocationsAsync(DATA_SOURCE_ID, INBOX, [7], TOKEN);
        var nextRunStart = DateTimeOffset.UtcNow;

        var dueThisRun = await this.Client.GetOrphanedMailsAsync(DATA_SOURCE_ID, runStart, TOKEN);
        var dueNextRun = await this.Client.GetOrphanedMailsAsync(DATA_SOURCE_ID, nextRunStart, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(dueThisRun, Is.Empty, "The run which lost the mail may still find it in a folder it gets to later.");
            Assert.That(dueNextRun, Is.EqualTo(new[] { KEY_A }));
        });
    }

    [Test]
    public async Task ANewUidValidityVoidsTheUidsOfItsFolderButKeepsTheMails()
    {
        var onlyInInbox = await this.StoreDocumentAsync(KEY_A);
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(onlyInInbox, At(INBOX, 7)), TOKEN);
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(await this.StoreDocumentAsync(KEY_B), At(INBOX, 8), At(ARCHIVE, 3)), TOKEN);

        await this.Client.UpsertMailFolderAsync(DATA_SOURCE_ID, Folder(INBOX, 2), TOKEN);

        var inboxLocations = await this.Client.GetMailLocationsAsync(DATA_SOURCE_ID, INBOX, TOKEN);
        var archiveLocations = await this.Client.GetMailLocationsAsync(DATA_SOURCE_ID, ARCHIVE, TOKEN);
        var orphaned = await this.Client.GetOrphanedMailsAsync(DATA_SOURCE_ID, DateTimeOffset.UtcNow.AddMinutes(1), TOKEN);
        var orphan = await this.Client.GetMailAsync(DATA_SOURCE_ID, onlyInInbox, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(inboxLocations, Is.Empty, "Under a new UIDVALIDITY, the stored UIDs name nothing any more.");
            Assert.That(archiveLocations.Keys, Is.EquivalentTo(new long[] { 3 }), "Other folders are not affected.");
            Assert.That(orphaned, Is.EqualTo(new[] { KEY_A }), "Only the mail which lies nowhere else is an orphan.");
            Assert.That(orphan, Is.Not.Null, "Orphaned, not deleted: found again under its new UID, it is only linked anew.");
        });
    }

    [Test]
    public async Task ARemovedFolderOrphansOnlyTheMailsWhichLieNowhereElse()
    {
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(await this.StoreDocumentAsync(KEY_A), At(INBOX, 7)), TOKEN);
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(await this.StoreDocumentAsync(KEY_B), At(INBOX, 8), At(ARCHIVE, 3)), TOKEN);

        await this.Client.DeleteMailFolderAsync(DATA_SOURCE_ID, INBOX, TOKEN);

        var folders = await this.Client.GetMailFoldersAsync(DATA_SOURCE_ID, TOKEN);
        var orphaned = await this.Client.GetOrphanedMailsAsync(DATA_SOURCE_ID, DateTimeOffset.UtcNow.AddMinutes(1), TOKEN);
        Assert.Multiple(() =>
        {
            Assert.That(folders.Select(folder => folder.Path), Is.EqualTo(new[] { ARCHIVE }));
            Assert.That(orphaned, Is.EqualTo(new[] { KEY_A }));
        });
    }

    [Test]
    public async Task FlagsChangeOnlyWhereTheyWereSet()
    {
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(await this.StoreDocumentAsync(KEY_A), At(INBOX, 7), At(ARCHIVE, 3)), TOKEN);

        await this.Client.UpdateMailFlagsAsync(DATA_SOURCE_ID, INBOX, new Dictionary<long, MailFlags> { [7] = new(true, true, false), [99] = new(true, false, false) }, TOKEN);

        var inboxLocations = await this.Client.GetMailLocationsAsync(DATA_SOURCE_ID, INBOX, TOKEN);
        var archiveLocations = await this.Client.GetMailLocationsAsync(DATA_SOURCE_ID, ARCHIVE, TOKEN);
        Assert.Multiple(() =>
        {
            Assert.That(inboxLocations, Is.EqualTo(new Dictionary<long, MailFlags> { [7] = new(true, true, false) }), "A UID the index does not hold is ignored, not added.");
            Assert.That(archiveLocations, Is.EqualTo(new Dictionary<long, MailFlags> { [3] = new(false, false, false) }), "IMAP keeps flags per folder, so the copy in the archive keeps its own.");
        });
    }

    [Test]
    public async Task AUidNamesOneMailOnly()
    {
        var first = await this.StoreDocumentAsync(KEY_A);
        var second = await this.StoreDocumentAsync(KEY_B);
        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(first, At(INBOX, 7)), TOKEN);

        await this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(second, At(INBOX, 7)), TOKEN);

        var firstStored = await this.Client.GetMailAsync(DATA_SOURCE_ID, first, TOKEN);
        var secondStored = await this.Client.GetMailAsync(DATA_SOURCE_ID, second, TOKEN);
        var orphaned = await this.Client.GetOrphanedMailsAsync(DATA_SOURCE_ID, DateTimeOffset.UtcNow.AddMinutes(1), TOKEN);
        Assert.Multiple(() =>
        {
            Assert.That(firstStored!.Locations, Is.Empty, "The server says another mail lies there now, so the stored place was stale.");
            Assert.That(secondStored!.Locations, Is.EqualTo(new[] { At(INBOX, 7) }));
            Assert.That(orphaned, Is.EqualTo(new[] { KEY_A }));
        });
    }

    [Test]
    public async Task AMailIsOnlyStoredWithAPlaceInAStoredFolder()
    {
        var mailId = await this.StoreDocumentAsync(KEY_A);

        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<ArgumentException>(() => this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(mailId), TOKEN), "A mail which lies nowhere would be an orphan from the start.");
            Assert.ThrowsAsync<ArgumentException>(() => this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(mailId, At(INBOX, 7), At(INBOX, 7)), TOKEN));
            Assert.ThrowsAsync<InvalidOperationException>(() => this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(mailId, At("Unknown", 1)), TOKEN));
            Assert.ThrowsAsync<ArgumentException>(() => this.Client.UpsertMailAsync(DATA_SOURCE_ID, Mail(mailId, At(INBOX, 7)) with { ReferenceMessageIds = ["one two@example.org"] }, TOKEN), "Stored separated by spaces, this one would come back as two.");
        });
    }

    /// <summary>
    /// Stores the document a mail rests on, as indexing does before it stores the mail.
    /// </summary>
    private async Task<string> StoreDocumentAsync(string mailKey)
    {
        var mailId = IndexedDocumentIds.CreateParentId(DATA_SOURCE_ID, mailKey);
        var now = DateTimeOffset.UtcNow;
        await this.Client.UpsertFileAsync(DATA_SOURCE_ID, new EmbeddingStateFile(mailId, mailKey, "Quarterly report", mailKey, "mail", "fingerprint", 2048, now, now, now, 1), TOKEN);
        return mailId;
    }

    private static MailFolderRecord Folder(string path, long uidValidity) => new(path, MailFolderSpecialUse.NONE, uidValidity, null, null, null, null, null);

    private static MailLocationRecord At(string folderPath, long uid, bool isSeen = false) => new(folderPath, uid, new MailFlags(isSeen, false, false));

    private static MailRecord Mail(string mailId, params MailLocationRecord[] locations) => new(
        mailId,
        "report@example.org",
        "draft@example.org",
        ["kickoff@example.org", "draft@example.org"],
        new DateTimeOffset(2026, 9, 30, 14, 5, 0, TimeSpan.FromHours(2)),
        new DateTimeOffset(2026, 9, 30, 12, 5, 3, TimeSpan.Zero),
        MailImportance.HIGH,
        MailEncryptionKind.NONE,
        "mail-hash",
        FIRST_SEEN,
        [
            new MailAddressRecord(MailAddressRole.FROM, "alice@example.org", "Alice"),
            new MailAddressRecord(MailAddressRole.TO, "bob@example.org", "Bob"),
            new MailAddressRecord(MailAddressRole.TO, "carol@example.org", string.Empty),
            new MailAddressRecord(MailAddressRole.CC, "dave@example.org", "Dave"),
        ],
        [
            new MailPartRecord(MailPartKind.HEADERS, string.Empty, "text/rfc822-headers", 812, "From: Alice <alice@example.org>", MailPartTextState.EXTRACTED),
            new MailPartRecord(MailPartKind.BODY, string.Empty, "text/html", 2048, "The report.", MailPartTextState.EXTRACTED),
            new MailPartRecord(MailPartKind.ATTACHMENT, "report.pdf", "application/pdf", 9_000_000, null, MailPartTextState.TOO_LARGE),
        ],
        locations);
}