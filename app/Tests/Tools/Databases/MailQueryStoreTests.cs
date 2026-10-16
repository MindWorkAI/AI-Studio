using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks how the mail tools find, count and summarize mails in the index.
/// </summary>
/// <remarks>
/// The conditions come from the model, and the user relies on them: "unread mails from Bob" that
/// also lists read ones, or a mail from another mailbox, is a wrong answer told with confidence.
/// So each condition is checked on its own against a small mailbox, together with the two mails
/// which must never show up: one which lies nowhere any more, and one of another mailbox.
/// </remarks>
[TestFixture]
public sealed class MailQueryStoreTests
{
    private const string MAILBOX = "0c3f9b52-7d4e-4a1b-9e6f-2b8c5d7a1e40";
    private const string OTHER_MAILBOX = "5a1e9c37-2b4d-4e8f-a6c0-3d7b9e1f2a48";
    private const string INBOX = "INBOX";
    private const string ARCHIVE = "Archive/2026";

    private const string REPORT = "report";
    private const string BUDGET = "budget";
    private const string NEWSLETTER = "newsletter";
    private const string DELETED = "deleted";
    private const string ELSEWHERE = "elsewhere";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    private readonly Dictionary<string, string> mailIds = new(StringComparer.Ordinal);

    private TemporaryIndexStore store = null!;

    private IndexStoreClient Client => this.store.Client;

    [SetUp]
    public async Task CreateMailboxesAsync()
    {
        this.mailIds.Clear();
        this.store = await TemporaryIndexStore.CreateAsync();
        foreach (var dataSourceId in new[] { MAILBOX, OTHER_MAILBOX })
        {
            await this.Client.UpsertDataSourceAsync(dataSourceId, "MAILBOX", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);
            await this.Client.UpsertMailFolderAsync(dataSourceId, new MailFolderRecord(INBOX, MailFolderSpecialUse.NONE, 1, null, null, null, null, null), TOKEN);
        }

        await this.Client.UpsertMailFolderAsync(MAILBOX, new MailFolderRecord(ARCHIVE, MailFolderSpecialUse.ARCHIVE, 1, null, null, null, null, null), TOKEN);

        await this.StoreMailAsync(MAILBOX, REPORT, "Quarterly report", "The quarterly report is ready for review.", Mail(
            new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
            [From("alice@example.org", "Alice Müller"), To("bob@example.org", "Bob")],
            [At(INBOX, 1, isSeen: true)]) with { MessageId = "report@example.org" });

        await this.StoreMailAsync(MAILBOX, BUDGET, "Budget 2027", "Comments on the report and the budget.", Mail(
            new DateTimeOffset(2026, 9, 10, 9, 30, 0, TimeSpan.Zero),
            [From("bob@example.org", "Bob"), To("alice@example.org", "Alice Müller"), Cc("carol@example.org", "Carol")],
            [new MailLocationRecord(INBOX, 2, new MailFlags(false, true, false))]) with
        {
            MessageId = "budget@example.org",
            InReplyTo = "report@example.org",
            Importance = MailImportance.HIGH,
            Parts = [new MailPartRecord(MailPartKind.ATTACHMENT, "budget.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 52_000, "Budget", MailPartTextState.EXTRACTED)],
        });

        await this.StoreMailAsync(MAILBOX, NEWSLETTER, "Newsletter August", "Content encrypted.", Mail(
            new DateTimeOffset(2026, 8, 15, 7, 0, 0, TimeSpan.Zero),
            [From("news@lists.example.org", "Example News"), To("alice@example.org", "Alice Müller")],
            [At(ARCHIVE, 5, isSeen: true)]) with { EncryptionKind = MailEncryptionKind.SMIME });

        await this.StoreMailAsync(MAILBOX, DELETED, "Old report", "The report of a deleted mail.", Mail(
            new DateTimeOffset(2026, 9, 20, 7, 0, 0, TimeSpan.Zero),
            [From("alice@example.org", "Alice Müller")],
            [At(INBOX, 3)]) with { MessageId = "deleted@example.org" });

        await this.Client.RemoveMailLocationsAsync(MAILBOX, INBOX, [3], TOKEN);

        await this.StoreMailAsync(OTHER_MAILBOX, ELSEWHERE, "Quarterly report", "The report in another mailbox.", Mail(
            new DateTimeOffset(2026, 9, 25, 7, 0, 0, TimeSpan.Zero),
            [From("alice@example.org", "Alice Müller")],
            [At(INBOX, 1)]) with { MessageId = "report@example.org" });
    }

    [TearDown]
    public async Task DeleteMailboxesAsync() => await this.store.DisposeAsync();

    private static IEnumerable<TestCaseData> Conditions()
    {
        yield return Case("no condition", new MailFilter(), BUDGET, REPORT, NEWSLETTER);
        yield return Case("from, by address", new MailFilter { From = "alice" }, REPORT);
        yield return Case("from, by name", new MailFilter { From = "Müller" }, REPORT);
        yield return Case("from, in other case", new MailFilter { From = "BOB@EXAMPLE.ORG" }, BUDGET);
        yield return Case("to, including Cc", new MailFilter { To = "carol" }, BUDGET);
        yield return Case("to, several mails", new MailFilter { To = "alice" }, BUDGET, NEWSLETTER);
        yield return Case("received since, inclusive", new MailFilter { ReceivedSinceUtc = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero) }, BUDGET, REPORT);
        yield return Case("received before, exclusive", new MailFilter { ReceivedBeforeUtc = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero) }, NEWSLETTER);
        yield return Case("received since, in another time zone", new MailFilter { ReceivedSinceUtc = new DateTimeOffset(2026, 9, 1, 10, 0, 1, TimeSpan.FromHours(2)) }, BUDGET);
        yield return Case("unread", new MailFilter { IsUnread = true }, BUDGET);
        yield return Case("read", new MailFilter { IsUnread = false }, REPORT, NEWSLETTER);
        yield return Case("flagged", new MailFilter { IsFlagged = true }, BUDGET);
        yield return Case("not flagged", new MailFilter { IsFlagged = false }, REPORT, NEWSLETTER);
        yield return Case("encrypted", new MailFilter { IsEncrypted = true }, NEWSLETTER);
        yield return Case("not encrypted", new MailFilter { IsEncrypted = false }, BUDGET, REPORT);
        yield return Case("high importance", new MailFilter { Importance = MailImportance.HIGH }, BUDGET);
        yield return Case("normal importance", new MailFilter { Importance = MailImportance.NORMAL }, REPORT, NEWSLETTER);
        yield return Case("with attachments", new MailFilter { HasAttachments = true }, BUDGET);
        yield return Case("without attachments", new MailFilter { HasAttachments = false }, REPORT, NEWSLETTER);
        yield return Case("in a folder", new MailFilter { FolderPaths = [ARCHIVE] }, NEWSLETTER);
        yield return Case("in no folder at all", new MailFilter { FolderPaths = [] });
        yield return Case("both conditions, none meets them", new MailFilter { From = "alice", IsUnread = true });
        yield return Case("both conditions, one meets them", new MailFilter { From = "bob", HasAttachments = true }, BUDGET);
        yield return Case("a percent sign is no wildcard", new MailFilter { From = "%" });
        yield return Case("an underscore is no wildcard", new MailFilter { To = "b_b" });
    }

    [TestCaseSource(nameof(Conditions))]
    public async Task EachConditionNarrowsTheMailsNewestFirst(MailFilter filter, string[] expectedMails)
    {
        var found = await this.Client.QueryMailsAsync(MAILBOX, filter, 0, 10, TOKEN);

        Assert.That(found, Is.EqualTo(this.IdsOf(expectedMails)), "A mail which lies nowhere any more, or which belongs to another mailbox, must never show up.");
    }

    [Test]
    public async Task PagesFollowEachOtherWithoutGapsOrRepeats()
    {
        var first = await this.Client.QueryMailsAsync(MAILBOX, new MailFilter(), 0, 2, TOKEN);
        var second = await this.Client.QueryMailsAsync(MAILBOX, new MailFilter(), 2, 2, TOKEN);
        var beyond = await this.Client.QueryMailsAsync(MAILBOX, new MailFilter(), 3, 2, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(this.IdsOf(BUDGET, REPORT)));
            Assert.That(second, Is.EqualTo(this.IdsOf(NEWSLETTER)));
            Assert.That(beyond, Is.Empty);
        });
    }

    [Test]
    public async Task ACopyReadInAnyFolderMakesTheMailRead()
    {
        await this.Client.AddMailLocationAsync(MAILBOX, this.mailIds[BUDGET], At(ARCHIVE, 9, isSeen: true), TOKEN);

        var unread = await this.Client.QueryMailsAsync(MAILBOX, new MailFilter { IsUnread = true }, 0, 10, TOKEN);
        var summary = (await this.Client.GetMailSummariesAsync(MAILBOX, [this.mailIds[BUDGET]], TOKEN)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(unread, Is.Empty, "Whoever read the copy in the archive read the mail.");
            Assert.That(summary.Flags, Is.EqualTo(new MailFlags(true, true, false)));
            Assert.That(summary.FolderPaths, Is.EqualTo(new[] { ARCHIVE, INBOX }));
        });
    }

    [Test]
    public async Task TheChunkIdsAreThoseOfTheMatchingMailsOnly()
    {
        var flagged = await this.Client.GetMailChunkIdsAsync(MAILBOX, new MailFilter { IsFlagged = true }, TOKEN);
        var all = await this.Client.GetMailChunkIdsAsync(MAILBOX, new MailFilter(), TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(flagged, Is.EqualTo(new[] { ChunkIdOf(MAILBOX, BUDGET) }));
            Assert.That(all, Is.EquivalentTo(new[] { ChunkIdOf(MAILBOX, REPORT), ChunkIdOf(MAILBOX, BUDGET), ChunkIdOf(MAILBOX, NEWSLETTER) }), "The chunks of the deleted mail are still in the vector store, but must not be searched.");
        });
    }

    [Test]
    public async Task TheFullTextSearchStaysWithinTheMailboxAndTheConditions()
    {
        var everywhere = await this.Client.SearchMailChunksAsync(MAILBOX, "report", new MailFilter(), 10, TOKEN);
        var flagged = await this.Client.SearchMailChunksAsync(MAILBOX, "report", new MailFilter { IsFlagged = true }, 10, TOKEN);
        var bySubject = await this.Client.SearchMailChunksAsync(MAILBOX, "newsletter", new MailFilter(), 10, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(everywhere.Select(result => result.ParentFileId), Is.EquivalentTo(this.IdsOf(REPORT, BUDGET)));
            Assert.That(flagged.Select(result => result.ParentFileId), Is.EqualTo(this.IdsOf(BUDGET)));
            Assert.That(bySubject.Select(result => result.ParentFileId), Is.EqualTo(this.IdsOf(NEWSLETTER)), "The subject is searched along with the text.");
        });
    }

    [Test]
    public async Task CountsFollowTheConditions()
    {
        var total = await this.Client.CountMailsAsync(MAILBOX, new MailFilter(), MailCountGrouping.NONE, 20, TOKEN);
        var byFolder = await this.Client.CountMailsAsync(MAILBOX, new MailFilter(), MailCountGrouping.FOLDER, 20, TOKEN);
        var bySender = await this.Client.CountMailsAsync(MAILBOX, new MailFilter(), MailCountGrouping.SENDER, 2, TOKEN);
        var flaggedBySender = await this.Client.CountMailsAsync(MAILBOX, new MailFilter { IsFlagged = true }, MailCountGrouping.SENDER, 20, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(total.TotalCount, Is.EqualTo(3));
            Assert.That(total.Groups, Is.Empty);
            Assert.That(byFolder.TotalCount, Is.EqualTo(3));
            Assert.That(byFolder.Groups, Is.EqualTo(new MailCountGroup[] { new(INBOX, string.Empty, 2), new(ARCHIVE, string.Empty, 1) }), "Largest group first.");
            Assert.That(bySender.Groups, Is.EqualTo(new MailCountGroup[] { new("alice@example.org", "Alice Müller", 1), new("bob@example.org", "Bob", 1) }), "Groups of the same size by their key, and no more than asked for.");
            Assert.That(flaggedBySender.TotalCount, Is.EqualTo(1));
            Assert.That(flaggedBySender.Groups, Is.EqualTo(new MailCountGroup[] { new("bob@example.org", "Bob", 1) }));
        });
    }

    [Test]
    public async Task AMailInTwoFoldersCountsOnceInTotalButInEachFolder()
    {
        await this.Client.AddMailLocationAsync(MAILBOX, this.mailIds[BUDGET], At(ARCHIVE, 9), TOKEN);

        var byFolder = await this.Client.CountMailsAsync(MAILBOX, new MailFilter(), MailCountGrouping.FOLDER, 20, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(byFolder.TotalCount, Is.EqualTo(3));
            Assert.That(byFolder.Groups, Is.EqualTo(new MailCountGroup[] { new(ARCHIVE, string.Empty, 2), new(INBOX, string.Empty, 2) }));
        });
    }

    [Test]
    public async Task ASummaryShowsWhatAListOfMailsNeeds()
    {
        var requested = new[] { this.mailIds[BUDGET], "no-such-mail", this.mailIds[DELETED], this.mailIds[REPORT], this.mailIds[ELSEWHERE] };

        var summaries = await this.Client.GetMailSummariesAsync(MAILBOX, requested, TOKEN);

        Assert.That(summaries.Select(summary => summary.MailId), Is.EqualTo(this.IdsOf(BUDGET, REPORT)), "In the order asked for, without unknown mails, deleted ones, or those of another mailbox.");
        var budget = summaries[0];
        Assert.Multiple(() =>
        {
            Assert.That(budget.Subject, Is.EqualTo("Budget 2027"));
            Assert.That(budget.ReceivedAtUtc, Is.EqualTo(new DateTimeOffset(2026, 9, 10, 9, 30, 0, TimeSpan.Zero)));
            Assert.That(budget.MessageId, Is.EqualTo("budget@example.org"));
            Assert.That(budget.InReplyTo, Is.EqualTo("report@example.org"));
            Assert.That(budget.Addresses, Is.EqualTo(new[] { From("bob@example.org", "Bob"), To("alice@example.org", "Alice Müller"), Cc("carol@example.org", "Carol") }));
            Assert.That(budget.FolderPaths, Is.EqualTo(new[] { INBOX }));
            Assert.That(budget.Flags, Is.EqualTo(new MailFlags(false, true, false)));
            Assert.That(budget.Importance, Is.EqualTo(MailImportance.HIGH));
            Assert.That(budget.EncryptionKind, Is.EqualTo(MailEncryptionKind.NONE));
            Assert.That(budget.AttachmentNames, Is.EqualTo(new[] { "budget.xlsx" }));
        });
    }

    [Test]
    public async Task AReplyFindsTheMailItAnswersInItsOwnMailbox()
    {
        var answered = await this.Client.FindMailByMessageIdAsync(MAILBOX, "report@example.org", TOKEN);
        var answeredElsewhere = await this.Client.FindMailByMessageIdAsync(OTHER_MAILBOX, "report@example.org", TOKEN);
        var deleted = await this.Client.FindMailByMessageIdAsync(MAILBOX, "deleted@example.org", TOKEN);
        var none = await this.Client.FindMailByMessageIdAsync(MAILBOX, string.Empty, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(answered, Is.EqualTo(this.mailIds[REPORT]));
            Assert.That(answeredElsewhere, Is.EqualTo(this.mailIds[ELSEWHERE]), "The same Message-ID in another mailbox is that mailbox's own copy.");
            Assert.That(deleted, Is.Null);
            Assert.That(none, Is.Null, "Mails without a Message-ID must not all answer each other.");
        });
    }

    private async Task StoreMailAsync(string dataSourceId, string name, string subject, string text, MailRecord mail) =>
        this.mailIds[name] = await this.store.StoreMailAsync(dataSourceId, name, subject, text, mail);

    private string[] IdsOf(params string[] names) => names.Select(name => this.mailIds[name]).ToArray();

    private static string ChunkIdOf(string dataSourceId, string name) => IndexedDocumentIds.CreateChunkId(dataSourceId, name, 0);

    private static TestCaseData Case(string name, MailFilter filter, params string[] expectedMails) => new TestCaseData(filter, expectedMails).SetArgDisplayNames(name);

    private static MailAddressRecord From(string address, string displayName) => new(MailAddressRole.FROM, address, displayName);

    private static MailAddressRecord To(string address, string displayName) => new(MailAddressRole.TO, address, displayName);

    private static MailAddressRecord Cc(string address, string displayName) => new(MailAddressRole.CC, address, displayName);

    private static MailLocationRecord At(string folderPath, long uid, bool isSeen = false) => new(folderPath, uid, new MailFlags(isSeen, false, false));

    private static MailRecord Mail(DateTimeOffset received, IReadOnlyList<MailAddressRecord> addresses, IReadOnlyList<MailLocationRecord> locations) => new(
        string.Empty,
        string.Empty,
        string.Empty,
        [],
        received,
        received,
        MailImportance.NORMAL,
        MailEncryptionKind.NONE,
        "mail-hash",
        received,
        addresses,
        [],
        locations);
}