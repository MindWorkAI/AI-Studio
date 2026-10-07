using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks which mailboxes the mail tools may read, and what they get from the index.
/// </summary>
/// <remarks>
/// A mailbox holds what its owner wrote and received, so a provider below its level must never see
/// a word of it: neither the provider of the chat, which reads what is found, nor the embedding
/// provider, which gets the query. Within a mailbox, the tools rely on pages which neither repeat
/// nor skip a mail, and on a reply leading to the mail it answers.
/// </remarks>
[TestFixture]
public sealed class MailboxRetrievalTests
{
    private const string MAILBOX = "3d8f1a6c-9b2e-4c7d-a5f0-6e1b8c4d2a97";
    private const string OTHER_MAILBOX = "8b2c6e1f-4a9d-4f3b-b7e5-1c0d9a6f3e82";
    private const string INBOX = "INBOX";

    private static readonly CancellationToken TOKEN = CancellationToken.None;
    private static readonly DataSourceMailbox WORK = new() { Id = MAILBOX, Name = "Work", MaxAge = MailboxMaxAge.LAST_6_MONTHS };
    private static readonly DataSourceMailbox PRIVATE = new() { Id = OTHER_MAILBOX, Name = "Private", MaxAge = MailboxMaxAge.ALL };

    private TemporaryIndexStore store = null!;
    private string question = string.Empty;
    private string answer = string.Empty;
    private string newsletter = string.Empty;
    private string deleted = string.Empty;
    private string elsewhere = string.Empty;

    [SetUp]
    public async Task CreateMailboxesAsync()
    {
        this.store = await TemporaryIndexStore.CreateAsync();
        foreach (var mailboxId in new[] { MAILBOX, OTHER_MAILBOX })
        {
            await this.store.Client.UpsertDataSourceAsync(mailboxId, "MAILBOX", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);
            await this.store.Client.UpsertMailFolderAsync(mailboxId, new MailFolderRecord(INBOX, MailFolderSpecialUse.NONE, 1, null, null, 120, 4, null), TOKEN);
        }

        this.question = await this.store.StoreMailAsync(MAILBOX, "question", "Question", "Could you have a look at the draft?", Mail(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), At(1, isSeen: true)) with { MessageId = "question@example.org" });
        this.answer = await this.store.StoreMailAsync(MAILBOX, "answer", "Re: Question", "I had a look, see my comments.", Mail(new DateTimeOffset(2026, 9, 10, 9, 30, 0, TimeSpan.Zero), At(2, isSeen: false)) with { MessageId = "answer@example.org", InReplyTo = "question@example.org" });
        this.newsletter = await this.store.StoreMailAsync(MAILBOX, "newsletter", "Newsletter August", "What happened in August.", Mail(new DateTimeOffset(2026, 8, 15, 7, 0, 0, TimeSpan.Zero), At(3, isSeen: true)));
        this.deleted = await this.store.StoreMailAsync(MAILBOX, "deleted", "Old draft", "A mail which is gone from the server.", Mail(new DateTimeOffset(2026, 9, 20, 7, 0, 0, TimeSpan.Zero), At(4, isSeen: false)));
        await this.store.Client.RemoveMailLocationsAsync(MAILBOX, INBOX, [4], TOKEN);

        this.elsewhere = await this.store.StoreMailAsync(OTHER_MAILBOX, "elsewhere", "Holidays", "A mail in another mailbox.", Mail(new DateTimeOffset(2026, 9, 25, 7, 0, 0, TimeSpan.Zero), At(1, isSeen: false)));
    }

    [TearDown]
    public async Task DeleteMailboxesAsync() => await this.store.DisposeAsync();

    [TestCase(ConfidenceLevel.MEDIUM, ConfidenceLevel.HIGH, ConfidenceLevel.HIGH, true)]
    [TestCase(ConfidenceLevel.MEDIUM, ConfidenceLevel.MEDIUM, ConfidenceLevel.MEDIUM, true)]
    [TestCase(ConfidenceLevel.MEDIUM, ConfidenceLevel.LOW, ConfidenceLevel.HIGH, false)]
    [TestCase(ConfidenceLevel.MEDIUM, ConfidenceLevel.HIGH, ConfidenceLevel.LOW, false)]
    public void BothProvidersHaveToMeetTheLevelOfTheMailbox(ConfidenceLevel mailboxLevel, ConfidenceLevel chatProviderConfidence, ConfidenceLevel embeddingProviderConfidence, bool expected)
    {
        Assert.That(MailboxRetrievalService.IsReadable(mailboxLevel, chatProviderConfidence, embeddingProviderConfidence), Is.EqualTo(expected));
    }

    [TestCase(ConfidenceLevel.NONE)]
    [TestCase(ConfidenceLevel.UNTRUSTED)]
    [TestCase(ConfidenceLevel.UNKNOWN)]
    public void AMailboxWithoutAValidLevelStaysClosed(ConfidenceLevel mailboxLevel)
    {
        Assert.That(MailboxRetrievalService.IsReadable(mailboxLevel, ConfidenceLevel.HIGH, ConfidenceLevel.HIGH), Is.False, "These levels would let almost every provider through.");
    }

    [Test]
    public void AMailboxWithoutItsEmbeddingProviderStaysClosed()
    {
        Assert.That(MailboxRetrievalService.IsReadable(ConfidenceLevel.MEDIUM, ConfidenceLevel.HIGH, embeddingProviderConfidence: null), Is.False, "Nobody can tell whether a replacement would meet the level.");
    }

    [Test]
    public void EveryMailIsListedOnceWithItsBestPassage()
    {
        var passages = MailboxRetrievalService.BestPassagePerMail([new("a", "a1"), new("b", "b1"), new("a", "a2"), new("c", "c1"), new("b", "b2")]);

        Assert.That(passages, Is.EqualTo(new MailPassage[] { new("a", "a1"), new("b", "b1"), new("c", "c1") }));
    }

    [Test]
    public void AChannelWhichFilledItsWindowMayHoldMoreMails()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MailboxRetrievalService.MayHoldMoreMails(1, 10, 44, 44, 3), Is.True, "Its chunks may belong to fewer mails than the page needed.");
            Assert.That(MailboxRetrievalService.MayHoldMoreMails(1, 10, 44, 43, 3), Is.False, "Both channels found less than they were asked for, so they found everything.");
            Assert.That(MailboxRetrievalService.MayHoldMoreMails(9, 10, 364, 364, 364), Is.False, "Paging ends at the last page.");
        });
    }

    [Test]
    public async Task WithoutAQueryTheMailsArePagedNewestFirst()
    {
        var first = await MailboxRetrievalService.ListNewestFirstAsync(this.store.Client, MAILBOX, new MailFilter(), 1, 2, TOKEN);
        var second = await MailboxRetrievalService.ListNewestFirstAsync(this.store.Client, MAILBOX, new MailFilter(), 2, 2, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(first.Hits.Select(hit => hit.Summary.MailId), Is.EqualTo(new[] { this.answer, this.question }));
            Assert.That(first.HasMore, Is.True);
            Assert.That(second.Hits.Select(hit => hit.Summary.MailId), Is.EqualTo(new[] { this.newsletter }), "Neither the mail gone from the server nor the one of another mailbox shows up.");
            Assert.That(second.HasMore, Is.False);
            Assert.That(first.Hits.Concat(second.Hits).Select(hit => hit.Passage), Is.All.Null, "Without a query, no passage matched anything.");
        });
    }

    [Test]
    public async Task TheConditionsNarrowTheListedMails()
    {
        var unread = await MailboxRetrievalService.ListNewestFirstAsync(this.store.Client, MAILBOX, new MailFilter { IsUnread = true }, 1, 10, TOKEN);

        Assert.That(unread.Hits.Select(hit => hit.Summary.MailId), Is.EqualTo(new[] { this.answer }));
    }

    [Test]
    public async Task AReplyLeadsToTheMailItAnswers()
    {
        var reading = await MailboxRetrievalService.ReadMailAsync(this.store.Client, [WORK, PRIVATE], this.answer, TOKEN);

        Assert.That(reading, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(reading!.Mailbox.Id, Is.EqualTo(MAILBOX));
            Assert.That(reading.Summary.Subject, Is.EqualTo("Re: Question"));
            Assert.That(reading.Mail.MessageId, Is.EqualTo("answer@example.org"));
            Assert.That(reading.InReplyToMailId, Is.EqualTo(this.question));
        });
    }

    [Test]
    public async Task AMailIsReadOnlyFromTheMailboxesGiven()
    {
        var fromAnotherMailbox = await MailboxRetrievalService.ReadMailAsync(this.store.Client, [WORK], this.elsewhere, TOKEN);
        var fromItsOwnMailbox = await MailboxRetrievalService.ReadMailAsync(this.store.Client, [WORK, PRIVATE], this.elsewhere, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(fromAnotherMailbox, Is.Null, "The provider may not read the mailbox which holds it.");
            Assert.That(fromItsOwnMailbox?.Mailbox.Id, Is.EqualTo(OTHER_MAILBOX));
            Assert.That(fromItsOwnMailbox?.InReplyToMailId, Is.Null, "It answers no mail.");
        });
    }

    [Test]
    public async Task AMailGoneFromTheServerIsNotRead()
    {
        var goneFromTheServer = await MailboxRetrievalService.ReadMailAsync(this.store.Client, [WORK], this.deleted, TOKEN);
        var neverIndexed = await MailboxRetrievalService.ReadMailAsync(this.store.Client, [WORK], "4f1c9e2a-7b3d-4e8f-9a6c-2d5b8e1f3a70", TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(goneFromTheServer, Is.Null, "It lost its last place on the server.");
            Assert.That(neverIndexed, Is.Null, "Nobody ever indexed it.");
        });
    }

    [Test]
    public async Task AMailboxNeverSyncedKnowsOnlyItsPeriod()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        var coverage = await MailboxRetrievalService.ReadCoverageAsync(this.store.Client, WORK, now, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(coverage.ReceivedSinceUtc, Is.EqualTo(now.AddMonths(-6)));
            Assert.That(coverage.LastCompleteSyncUtc, Is.Null, "The first sync is still running.");
            Assert.That(coverage.SignInRefusedAtUtc, Is.Null);
            Assert.That(coverage.PendingRemovalCount, Is.Null);
            Assert.That(coverage.Folders.Select(folder => folder.Path), Is.EqualTo(new[] { INBOX }));
        });
    }

    [Test]
    public async Task TheCoverageTellsWhatTheIndexMisses()
    {
        var completed = new DateTimeOffset(2026, 10, 1, 9, 16, 0, TimeSpan.Zero);
        var refused = new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
        await this.store.Client.CompleteMailboxSyncAsync(OTHER_MAILBOX, completed, TOKEN);
        await this.store.Client.HoldBackMailRemovalAsync(OTHER_MAILBOX, 250, TOKEN);
        await this.store.Client.UpsertMailboxAuthFailureAsync(OTHER_MAILBOX, new MailboxAuthFailure(refused, "Invalid credentials."), TOKEN);

        var coverage = await MailboxRetrievalService.ReadCoverageAsync(this.store.Client, PRIVATE, refused, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(coverage.ReceivedSinceUtc, Is.Null, "The whole mailbox is indexed.");
            Assert.That(coverage.LastCompleteSyncUtc, Is.EqualTo(completed));
            Assert.That(coverage.SignInRefusedAtUtc, Is.EqualTo(refused), "No new mail arrives until the user deals with it.");
            Assert.That(coverage.PendingRemovalCount, Is.EqualTo(250));
        });
    }

    private static MailLocationRecord At(long uid, bool isSeen) => new(INBOX, uid, new MailFlags(isSeen, false, false));

    private static MailRecord Mail(DateTimeOffset received, MailLocationRecord location) => new(
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
        [new MailAddressRecord(MailAddressRole.FROM, "alice@example.org", "Alice")],
        [],
        [location]);
}