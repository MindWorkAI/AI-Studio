using System.Globalization;
using System.Text;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how Search Mails cuts what the mailboxes found into the pages the model reads.
/// </summary>
/// <remarks>
/// A search returns no more text than its budget allows, and nothing is kept between two searches.
/// Still, every mail a mailbox found has to reach the model on exactly one page, so that turning
/// pages brings up what did not fit before. FakeRuntime plays the filter for prompt injections, and
/// each test makes up what the mailboxes found, page by page.
/// </remarks>
[TestFixture]
public sealed class SearchMailsToolPagingTests
{
    /// <summary>
    /// What the filter removes. It is shorter than the marker which takes its place, so the filter makes a mail longer.
    /// </summary>
    private const string SUSPICIOUS_WORD = "PWNED";

    /// <summary>
    /// How many attachments a long mail lists.
    /// </summary>
    private const int ATTACHMENTS = 20;

    /// <summary>
    /// How long the name of each attachment of a long mail is: twenty long mails fit into a page, twenty-one do not.
    /// </summary>
    private const int ATTACHMENT_NAME_LENGTH = 230;

    /// <summary>
    /// How many pages a test turns at most before it gives up on the search ever ending.
    /// </summary>
    private const int MOST_PAGES = 100;

    private static readonly DateTimeOffset RECEIVED = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    // Pages of 30 mails, which leaves three pages of each mailbox:
    private static readonly DataSourceMailbox WORK = new() { Num = 1, Id = "9e4b2c7a-1d3f-4a8e-b6c5-2f0d7e1b3a64", Name = "Work", MaxMatches = 30 };
    private static readonly DataSourceMailbox PRIVATE = new() { Num = 2, Id = "4c8d1e6b-3a9f-4b2c-8e7d-5f1a0b9c2d36", Name = "Private", MaxMatches = 30 };

    private FakeRuntime runtime = null!;
    private RustService rustService = null!;
    private PromptInjectionGuardService guardService = null!;

    [SetUp]
    public void PrepareRuntime()
    {
        this.runtime = new FakeRuntime(SUSPICIOUS_WORD);
        this.rustService = new RustService(this.runtime);
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);
        this.guardService = new PromptInjectionGuardService(this.rustService, settingsManager, NullLogger<PromptInjectionGuardService>.Instance, NullLoggerFactory.Instance);
    }

    [TearDown]
    public void DisposeRuntime()
    {
        this.rustService.Dispose();
        this.runtime.Dispose();
    }

    [Test]
    public async Task EveryMailReachesTheModelOnExactlyOnePage()
    {
        var pages = await this.PageThroughAsync(Search(WORK), Found(Mails(1, 30, isLong: true), Mails(31, 30, isLong: true), Mails(61, 30, isLong: true)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages.SelectMany(NumbersOf), Is.EqualTo(Enumerable.Range(1, 90)), "Turning pages has to bring up every mail which did not fit before, in the order the mailbox ranked them.");
            Assert.That(pages.Select(CharactersOf), Has.All.LessThanOrEqualTo(SearchMailsTool.MAX_RESULT_CHARACTERS));
            Assert.That(pages.Select(page => page.Passages.Count), Is.EqualTo(pages.Select(page => NumbersOf(page).Count)), "The user is shown exactly the mails the model received.");
            Assert.That(pages.Select(page => page.Sources.Count), Is.EqualTo(pages.Select(page => NumbersOf(page).Count)));
        }
    }

    [Test]
    public async Task WhereEverythingFitsAPageIsWhatTheMailboxFound()
    {
        var pages = await this.PageThroughAsync(Search(WORK), Found(Mails(1, 30), Mails(31, 30)));

        Assert.That(pages.Select(NumbersOf), Is.EqualTo(new[] { Enumerable.Range(1, 30).ToList(), Enumerable.Range(31, 30).ToList() }));
    }

    [Test]
    public async Task AnEmptyPageOfTheMailboxDoesNotEndThePaging()
    {
        var pages = await this.PageThroughAsync(Search(WORK), Found(Mails(1, 30), [], Mails(31, 30)));

        Assert.That(pages.Select(NumbersOf), Is.EqualTo(new[] { Enumerable.Range(1, 30).ToList(), Enumerable.Range(31, 30).ToList() }), "A page of the mailbox can turn out empty, when its chunks belong to mails of the pages before, and the next one still holds mails.");
    }

    [Test]
    public async Task ThereIsNothingMoreWhenTheFurtherPagesOfTheMailboxAreEmpty()
    {
        var result = await this.SearchAsync([Search(WORK)], 1, Found(Mails(1, 30), []));

        Assert.That(result.Outcomes.Single().HasMore, Is.False, "The mailbox only said that its next page might hold more. The model must not be sent there for nothing.");
    }

    [Test]
    public async Task APageBeyondTheLastIsRefusedWithTheLastPage()
    {
        var found = Found(Mails(1, 30, isLong: true));
        var pages = await this.PageThroughAsync(Search(WORK), found);

        var message = (await Assert.ThrowsAsync<ArgumentException>(() => this.SearchAsync([Search(WORK)], pages.Count + 1, found)))!.Message;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages.Select(page => NumbersOf(page).Count), Is.EqualTo(new[] { 20, 10 }));
            Assert.That(message, Does.Contain("at most 2").And.Contain("but was 3"));
        }
    }

    [Test]
    public async Task SeveralMailboxesTakeTurnsUntilTheirNextMailNoLongerFits()
    {
        var work = Found(Mails(1, 30, isLong: true));
        var @private = Found([..Mails(101, 12, isLong: true), ..Mails(113, 18)]);

        var result = await this.SearchAsync([Search(WORK), Search(PRIVATE)], 1, (search, page) => search.Mailbox == WORK ? work(search, page) : @private(search, page));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcomes.Select(outcome => NumbersOf(outcome).ToList()), Is.EqualTo(new[] { Enumerable.Range(1, 10).ToList(), Enumerable.Range(101, 10).ToList() }), "The short mails of the private mailbox rank below one which did not fit, so they must not take its place.");
            Assert.That(result.Outcomes.Select(outcome => (outcome.Mails.Count, outcome.LeftOutCount, outcome.HasMore)), Is.EqualTo(new[] { (10, 20, true), (10, 20, true) }));
            Assert.That(CharactersOf(result), Is.LessThanOrEqualTo(SearchMailsTool.MAX_RESULT_CHARACTERS));
        }
    }

    [Test]
    public void ASearchOfSeveralMailboxesNamesTheWayToTheMailsLeftOut()
    {
        var description = SearchMailsTool.DescribeMailbox(new MailboxSearchOutcome(Search(WORK), [], true, [], 3, null), null, TimeZoneInfo.Utc);
        var issue = description["issues"]?.AsArray().Select(node => node!.GetValue<string>()).SingleOrDefault(text => text.Contains("did not fit", StringComparison.Ordinal));

        Assert.That(issue, Does.Contain("3 further mails of this mailbox did not fit into this search of several mailboxes").And.Contain("Search this mailbox alone, starting at page 1"));
    }

    [Test]
    public async Task MailsAreMeasuredAfterTheFilter()
    {
        // Twenty long mails fit into a page, but not once the filter put its markers in:
        var pages = await this.PageThroughAsync(Search(WORK), Found(Mails(1, 20, isLong: true, suspiciousWords: 3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages.Select(NumbersOf), Is.EqualTo(new[] { Enumerable.Range(1, 19).ToList(), [20] }));
            Assert.That(pages.Select(CharactersOf), Has.All.LessThanOrEqualTo(SearchMailsTool.MAX_RESULT_CHARACTERS));
        }
    }

    [Test]
    public async Task AMailboxWhichFailsOnAnEarlierPageCountsAsNotSearched()
    {
        var result = await this.SearchAsync([Search(WORK)], 2, (_, _) => Task.FromResult(MailSearchPage.EMPTY with { Gaps = [RetrievalGap.NOT_SEARCHED] }));
        var outcome = result.Outcomes.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome.Gaps, Is.EqualTo(new[] { RetrievalGap.NOT_SEARCHED }), "Without the pages before it, nobody knows where the page starts.");
            Assert.That(outcome.HasMore, Is.False);
            Assert.That(outcome.Mails, Is.Empty);
        }
    }

    [Test]
    public async Task AMailboxWithoutTheFolderNamedListsItsFoldersInsteadOfSearching()
    {
        var coverage = new MailboxCoverage(null, null, null, null, [new MailFolderRecord("INBOX", MailFolderSpecialUse.NONE, 1, null, null, null, null, null), new MailFolderRecord("Archive", MailFolderSpecialUse.NONE, 1, null, null, null, null, null)]);
        var search = new MailboxSearch(WORK, coverage, new MailFilter(), FolderIsMissing: true, MailDraftSyncDecision.NOT_NEEDED);

        var result = await this.SearchAsync([search], 1, (_, _) => throw new AssertionException("A mailbox without the folder named is not searched."));
        var outcome = result.Outcomes.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome.ListedFolders, Is.EquivalentTo(new[] { "INBOX", "Archive" }));
            Assert.That(outcome.Mails, Is.Empty);
            Assert.That(outcome.HasMore, Is.False);
        }
    }

    [Test]
    public async Task AMailAtItsLongestStaysFarBelowAPage()
    {
        // Every text far too long, of characters the model receives escaped, six for each of them:
        var hit = new MailSearchHit(
            new MailSummary(
                "mail-001",
                Escaped(10_000),
                RECEIVED,
                null,
                string.Empty,
                string.Empty,
                [new MailAddressRecord(MailAddressRole.FROM, Escaped(10_000), Escaped(10_000)), ..Enumerable.Range(1, 50).Select(_ => new MailAddressRecord(MailAddressRole.TO, Escaped(1_000), Escaped(1_000)))],
                [..Enumerable.Range(1, 50).Select(_ => Escaped(1_000))],
                new MailFlags(IsSeen: true, IsFlagged: false, IsAnswered: false),
                MailImportance.NORMAL,
                MailEncryptionKind.NONE,
                [..Enumerable.Range(1, 500).Select(_ => Escaped(1_000))]),
            Escaped(100_000),
            new ChunkLocation(AttachmentPosition: 499));

        var result = await this.SearchAsync([Search(WORK)], 1, Found([hit]));
        var mail = result.Outcomes.Single().Mails.SingleOrDefault();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(CharactersOf(result), Is.LessThan(SearchMailsTool.MAX_RESULT_CHARACTERS), "A single mail must never fill a page on its own.");
            Assert.That(mail?["more_recipients"]?.GetValue<int>(), Is.EqualTo(45));
            Assert.That(mail?["more_folders"]?.GetValue<int>(), Is.EqualTo(40));
            Assert.That(mail?["more_attachments"]?.GetValue<int>(), Is.EqualTo(480), "The model learns that there are more attachments, and read_mail lists them all.");
        }
    }

    [Test]
    public async Task AMailNamesTheAttachmentItsPassageIsFromAlsoBeyondTheListedOnes()
    {
        var mail = Mail(1, isLong: false, suspiciousWords: 0);
        var hit = mail with
        {
            Summary = mail.Summary with { AttachmentNames = [..Enumerable.Range(1, 30).Select(position => $"report-{position:D2}.txt")] },
            PassageLocation = new ChunkLocation(StartLine: 3, EndLine: 4, AttachmentPosition: 25),
        };

        var result = await this.SearchAsync([Search(WORK)], 1, Found([hit]));
        var description = result.Outcomes.Single().Mails.SingleOrDefault();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(description?["attachments"]?.AsArray(), Has.Count.EqualTo(20));
            Assert.That(description?["more_attachments"]?.GetValue<int>(), Is.EqualTo(10));
            Assert.That(description?["passage_location"]?.GetValue<string>(), Does.StartWith("attachment \"report-26.txt\""), "Otherwise, the model would take the passage for one of the text of the mail.");
            Assert.That(result.Sources.SingleOrDefault()?.Title, Does.Contain("report-26.txt"));
        }
    }

    /// <summary>
    /// Turns the pages of a search in one mailbox, until it says there is nothing more.
    /// </summary>
    private async Task<IReadOnlyList<SearchMailsResult>> PageThroughAsync(MailboxSearch search, Func<MailboxSearch, int, Task<MailSearchPage>> retrievePage)
    {
        var pages = new List<SearchMailsResult>();
        for (var page = 1; page <= MOST_PAGES; page++)
        {
            var result = await this.SearchAsync([search], page, retrievePage);
            pages.Add(result);
            if (!result.Outcomes.Single().HasMore)
                return pages;
        }

        Assert.Fail($"The search still had more after {MOST_PAGES} pages.");
        return pages;
    }

    private async Task<SearchMailsResult> SearchAsync(IReadOnlyList<MailboxSearch> searches, int page, Func<MailboxSearch, int, Task<MailSearchPage>> retrievePage)
    {
        await using var reportingScope = this.guardService.BeginAction();
        return await SearchMailsTool.SearchAsync(searches, page, retrievePage, this.guardService, reportingScope, TimeZoneInfo.Utc);
    }

    private static MailboxSearch Search(DataSourceMailbox mailbox) => new(mailbox, null, new MailFilter(), FolderIsMissing: false, MailDraftSyncDecision.NOT_NEEDED);

    /// <summary>
    /// Plays what a mailbox found, page by page: each page as given, and nothing after the last one.
    /// </summary>
    private static Func<MailboxSearch, int, Task<MailSearchPage>> Found(params IReadOnlyList<MailSearchHit>[] retrievalPages) => (_, page) => Task.FromResult(page <= retrievalPages.Length
        ? new MailSearchPage(retrievalPages[page - 1], page < retrievalPages.Length, [])
        : MailSearchPage.EMPTY);

    private static IReadOnlyList<MailSearchHit> Mails(int firstNumber, int count, bool isLong = false, int suspiciousWords = 0) =>
        Enumerable.Range(firstNumber, count).Select(number => Mail(number, isLong, suspiciousWords)).ToList();

    /// <summary>
    /// A mail whose ID ends in its number. A long one lists many attachments with long names, the first of which holds the suspicious word as often as asked for.
    /// </summary>
    private static MailSearchHit Mail(int number, bool isLong, int suspiciousWords)
    {
        IReadOnlyList<string> attachmentNames = isLong ? [..Enumerable.Range(1, ATTACHMENTS).Select(position => Text($"attachment-{position:D2} ", ATTACHMENT_NAME_LENGTH, position == 1 ? suspiciousWords : 0))] : [];
        var summary = new MailSummary(
            $"mail-{number:D3}",
            $"Mail {number:D3}",
            RECEIVED,
            null,
            string.Empty,
            string.Empty,
            [new MailAddressRecord(MailAddressRole.FROM, "anna@example.com", "Anna")],
            ["INBOX"],
            new MailFlags(IsSeen: true, IsFlagged: false, IsAnswered: false),
            MailImportance.NORMAL,
            MailEncryptionKind.NONE,
            attachmentNames);

        return new(summary, $"Passage of mail {number:D3}");
    }

    /// <summary>
    /// A text which starts as given, holds the suspicious word as often as asked for, and is as long as asked for.
    /// </summary>
    private static string Text(string start, int length, int suspiciousWords)
    {
        var text = new StringBuilder(start);
        for (var word = 0; word < suspiciousWords; word++)
            text.Append(SUSPICIOUS_WORD).Append(' ');

        text.Append('x', Math.Max(0, length - text.Length));
        return text.ToString();
    }

    /// <summary>
    /// A text of a control character, which JSON writes as an escape of six characters.
    /// </summary>
    private static string Escaped(int length) => new('\u0001', length);

    /// <summary>
    /// The numbers of the mails the model reads on a page, in the order it reads them.
    /// </summary>
    private static List<int> NumbersOf(SearchMailsResult result) => result.Outcomes.SelectMany(NumbersOf).ToList();

    private static IEnumerable<int> NumbersOf(MailboxSearchOutcome outcome) => outcome.Mails.Select(mail => int.Parse(mail["mail_id"]!.GetValue<string>()["mail-".Length..], CultureInfo.InvariantCulture));

    /// <summary>
    /// How many characters the mails of a page take, as the model receives them.
    /// </summary>
    private static int CharactersOf(SearchMailsResult result) => result.Outcomes.SelectMany(outcome => outcome.Mails).Sum(mail => mail.ToJsonString(ToolExecutionResult.MODEL_CONTENT_OPTIONS).Length);
}