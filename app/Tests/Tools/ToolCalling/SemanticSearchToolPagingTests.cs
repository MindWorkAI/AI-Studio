using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how Semantic Search cuts what the data sources found into the pages the model reads.
/// </summary>
/// <remarks>
/// A search returns no more text than its budget allows, and nothing is kept between two searches.
/// Still, every passage a data source found has to reach the model on exactly one page, so that
/// turning pages brings up what did not fit before. FakeRuntime plays the filter for prompt
/// injections, and each test makes up what the data sources found, page by page.
/// </remarks>
[TestFixture]
public sealed partial class SemanticSearchToolPagingTests
{
    /// <summary>
    /// What the filter removes. It is shorter than the marker which takes its place, so the filter makes a passage longer.
    /// </summary>
    private const string SUSPICIOUS_WORD = "PWNED";

    /// <summary>
    /// About the length of a chunk of 8,192 tokens: three such passages fill a search.
    /// </summary>
    private const int LONG = 30_000;

    /// <summary>
    /// A length of which a whole page of the data source fits into a search.
    /// </summary>
    private const int SHORT = 1_000;

    /// <summary>
    /// A length no page holds, so that a passage this long has to be shortened.
    /// </summary>
    private const int OVERLONG = 150_000;

    /// <summary>
    /// How many pages a test turns at most before it gives up on the search ever ending.
    /// </summary>
    private const int MOST_PAGES = 100;

    private static readonly IDataSource HANDBOOK = new DataSourceLocalDirectory { Num = 1, Id = "11111111-1111-1111-1111-111111111111", Name = "Handbook", MaxMatches = 10 };
    private static readonly IDataSource MINUTES = new DataSourceLocalFile { Num = 2, Id = "22222222-2222-2222-2222-222222222222", Name = "Minutes", MaxMatches = 10 };
    private static readonly IDataSource INTRANET = new DataSourceERI_V1 { Num = 3, Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = 10 };

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
    public async Task EveryPassageOfADataSourceReachesTheModelOnExactlyOnePage()
    {
        var pages = await this.PageThroughAsync(HANDBOOK, Found(Passages(1, 10, LONG), Passages(11, 10, LONG), Passages(21, 10, LONG)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages.SelectMany(NumbersOf), Is.EqualTo(Enumerable.Range(1, 30)), "Turning pages has to bring up every passage which did not fit before, in the order the data source ranked them.");
            Assert.That(pages.Select(page => page.TextContent.Length), Has.All.LessThanOrEqualTo(SemanticSearchTool.MAX_RESULT_CHARACTERS));
            Assert.That(pages.Select(page => page.Passages.Count), Is.EqualTo(pages.Select(page => NumbersOf(page).Count)), "The user is shown exactly the passages the model received.");
            Assert.That(pages.Select(page => page.Outcomes.Single().ResultCount), Is.EqualTo(pages.Select(page => NumbersOf(page).Count)));
        }
    }

    [Test]
    public async Task WhereEverythingFitsAPageIsWhatTheDataSourceFound()
    {
        var pages = await this.PageThroughAsync(HANDBOOK, Found(Passages(1, 10, SHORT), Passages(11, 10, SHORT)));

        Assert.That(pages.Select(NumbersOf), Is.EqualTo(new[] { Enumerable.Range(1, 10).ToList(), Enumerable.Range(11, 10).ToList() }));
    }

    [Test]
    public async Task AnEmptyPageOfTheDataSourceDoesNotEndThePaging()
    {
        var pages = await this.PageThroughAsync(HANDBOOK, Found(Passages(1, 10, SHORT), [], Passages(11, 10, SHORT)));

        Assert.That(pages.Select(NumbersOf), Is.EqualTo(new[] { Enumerable.Range(1, 10).ToList(), Enumerable.Range(11, 10).ToList() }), "A page of the data source can turn out empty, e.g., when its chunks have no text, and the next one still holds passages.");
    }

    [Test]
    public async Task ThereIsNothingMoreWhenTheFurtherPagesOfTheDataSourceAreEmpty()
    {
        var result = await this.SearchAsync([HANDBOOK], 1, Found(Passages(1, 10, SHORT), []));

        Assert.That(result.Outcomes.Single().HasMore, Is.False, "The data source only said that its next page might hold more. The model must not be sent there for nothing.");
    }

    [Test]
    public async Task APageBeyondTheLastIsRefusedWithTheLastPage()
    {
        var found = Found(Passages(1, 10, LONG));
        var pages = await this.PageThroughAsync(HANDBOOK, found);

        var message = (await Assert.ThrowsAsync<ArgumentException>(() => this.SearchAsync([HANDBOOK], pages.Count + 1, found)))!.Message;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages.Select(NumbersOf), Is.EqualTo(new[] { new List<int> { 1, 2, 3 }, [4, 5, 6], [7, 8, 9], [10] }));
            Assert.That(message, Does.Contain("at most 4").And.Contain("but was 5"));
        }
    }

    [Test]
    public async Task ThePassagesOfAFurtherPageGoOnWithTheirNumbers()
    {
        var result = await this.SearchAsync([HANDBOOK], 2, Found(Passages(1, 10, LONG)));

        Assert.That(result.TextContent, Does.StartWith($"# Retrieval context 4{Environment.NewLine}"), "The model reads the fourth passage of the search, not the first one a second time.");
    }

    [Test]
    public async Task AnERIDataSourcePagesWithinItsMatches()
    {
        var askedPages = new List<int>();
        var found = Found(Passages(1, 10, LONG));

        var pages = await this.PageThroughAsync(INTRANET, (dataSource, page) =>
        {
            askedPages.Add(page);
            return found(dataSource, page);
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages.SelectMany(NumbersOf), Is.EqualTo(Enumerable.Range(1, 10)));
            Assert.That(askedPages, Has.All.EqualTo(1), "ERI v1 knows no offset, so each page asks the server for the configured number of matches again.");
        }
    }

    [Test]
    public async Task SeveralDataSourcesTakeTurnsUntilTheirNextPassageNoLongerFits()
    {
        var handbook = Found(Passages(1, 10, LONG));
        var minutes = Found([..Passages(101, 2, LONG, "Minutes"), ..Passages(103, 8, SHORT, "Minutes")]);

        var result = await this.SearchAsync([HANDBOOK, MINUTES], 1, (dataSource, page) => dataSource == HANDBOOK ? handbook(dataSource, page) : minutes(dataSource, page));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(NumbersOf(result), Is.EqualTo(new[] { 1, 101, 2 }), "The short passages of the minutes rank below one which did not fit, so they must not take its place.");
            Assert.That(result.Outcomes.Select(outcome => (outcome.ResultCount, outcome.LeftOutCount, outcome.HasMore)), Is.EqualTo(new[] { (2, 8, true), (1, 9, true) }));
        }
    }

    [Test]
    public async Task PassagesAreMeasuredAfterTheFilter()
    {
        // Together, both fit into a search, but not once the filter put its marker in:
        const int LENGTH = 49_000;
        const int SUSPICIOUS_WORDS = 100;
        var pages = await this.PageThroughAsync(HANDBOOK, Found([Passage(1, LENGTH, suspiciousWords: SUSPICIOUS_WORDS), Passage(2, LENGTH, suspiciousWords: SUSPICIOUS_WORDS)]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages.Select(NumbersOf), Is.EqualTo(new[] { new List<int> { 1 }, [2] }));
            Assert.That(pages.Select(page => page.TextContent.Length), Has.All.LessThanOrEqualTo(SemanticSearchTool.MAX_RESULT_CHARACTERS));
        }
    }

    [Test]
    public async Task APassageLongerThanAPageIsShortenedToFillAPageOfItsOwn()
    {
        var pages = await this.PageThroughAsync(HANDBOOK, Found([Passage(1, SHORT), Passage(2, OVERLONG), Passage(3, SHORT)]));
        var shortened = pages[1];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages.Select(NumbersOf), Is.EqualTo(new[] { new List<int> { 1 }, [2], [3] }), "Neither the passage itself nor those after it may go missing.");
            Assert.That(shortened.TextContent.Length, Is.LessThanOrEqualTo(SemanticSearchTool.MAX_RESULT_CHARACTERS));
            Assert.That(shortened.TextContent, Does.Contain("Matched text content, shortened to fit the size limit:"), "The model has to know that the passage goes on.");
            Assert.That(shortened.Passages.Single().Text, Does.StartWith("Passage 002").And.Length.LessThan(OVERLONG), "The user is shown what the model read.");
            Assert.That(shortened.TextContent, Does.Contain(shortened.Passages.Single().Text));
        }
    }

    [Test]
    public async Task AShortenedPassageIsFilteredAgain()
    {
        var result = await this.SearchAsync([HANDBOOK], 1, Found([Passage(1, OVERLONG, suspiciousWords: 10)]));
        var shown = result.Passages.Single().Text;
        var filteredShorterTexts = this.runtime.FilteredTexts.Where(text => text.StartsWith("Passage 001", StringComparison.Ordinal) && text.Length < OVERLONG);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(filteredShorterTexts.Select(text => text.Replace(SUSPICIOUS_WORD, FakeRuntime.REDACTION_MARKER, StringComparison.Ordinal)), Has.Member(shown), "The model reads exactly what the filter returned for the shortened text, not a cut of what it returned for the whole one.");
            Assert.That(shown, Does.Contain(FakeRuntime.REDACTION_MARKER).And.Not.Contain(SUSPICIOUS_WORD));
            Assert.That(result.TextContent.Length, Is.LessThanOrEqualTo(SemanticSearchTool.MAX_RESULT_CHARACTERS));
        }
    }

    [Test]
    public async Task TheSurroundingContentIsShortenedBeforeTheMatchedText()
    {
        const int LENGTH = 40_000;
        var passage = Passage(1, LENGTH);
        passage.SurroundingContent = [Text("Before ", LENGTH), Text("After ", LENGTH)];

        var result = await this.SearchAsync([HANDBOOK], 1, Found([passage]));
        var shown = result.Passages.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shown.Text, Is.EqualTo(passage.MatchedText), "The matched text is what the search found, so it is the last to be shortened.");
            Assert.That(shown.SurroundingContent[0], Is.EqualTo(passage.SurroundingContent[0]));
            Assert.That(shown.SurroundingContent[^1], Does.StartWith("After ").And.Length.LessThan(LENGTH), "The surrounding content is shortened from its end.");
            Assert.That(result.TextContent, Does.Contain($"Matched text content:{Environment.NewLine}").And.Contain("Surrounding text content, shortened to fit the size limit:"));
            Assert.That(result.TextContent.Length, Is.LessThanOrEqualTo(SemanticSearchTool.MAX_RESULT_CHARACTERS));
        }
    }

    [Test]
    public async Task SurroundingContentWhichHadToGoIsNamed()
    {
        var passage = Passage(1, OVERLONG);
        passage.SurroundingContent = [Text("Before ", SHORT)];

        var result = await this.SearchAsync([HANDBOOK], 1, Found([passage]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Passages.Single().SurroundingContent, Is.Empty);
            Assert.That(result.TextContent, Does.Contain("Matched text content, shortened to fit the size limit:").And.Contain("Surrounding text content: left out to fit the size limit."), "Otherwise, the model would take the passage for one without any.");
        }
    }

    [Test]
    public async Task InASearchOfSeveralDataSourcesAShortenedPassageStillHasToFit()
    {
        var handbook = Found(Passages(1, 10, LONG));
        var minutes = Found([Passage(101, OVERLONG, "Minutes"), ..Passages(102, 9, SHORT, "Minutes")]);

        var result = await this.SearchAsync([HANDBOOK, MINUTES], 1, (dataSource, page) => dataSource == HANDBOOK ? handbook(dataSource, page) : minutes(dataSource, page));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(NumbersOf(result), Is.EqualTo(new[] { 1, 2, 3 }), "Shortened to a whole page, the best passage of the minutes no longer fits after the first of the handbook.");
            Assert.That(result.Outcomes.Select(outcome => (outcome.ResultCount, outcome.LeftOutCount, outcome.HasMore)), Is.EqualTo(new[] { (3, 7, true), (0, 10, true) }), "The minutes are left out with a way to them, searched alone.");
        }
    }

    [Test]
    public async Task ADataSourceWhichFailsOnAnEarlierPageCountsAsNotSearched()
    {
        var result = await this.SearchAsync([HANDBOOK], 2, (_, _) => Task.FromResult(RetrievalPage.EMPTY with { Gaps = [RetrievalGap.NOT_SEARCHED] }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcomes.Single().Gaps, Is.EqualTo(new[] { RetrievalGap.NOT_SEARCHED }), "Without the pages before it, nobody knows where the page starts.");
            Assert.That(result.Outcomes.Single().HasMore, Is.False);
            Assert.That(result.TextContent, Is.Empty);
        }
    }

    /// <summary>
    /// Turns the pages of a search in one data source, until it says there is nothing more.
    /// </summary>
    private async Task<IReadOnlyList<SemanticSearchResult>> PageThroughAsync(IDataSource dataSource, Func<IDataSource, int, Task<RetrievalPage>> retrievePage)
    {
        var pages = new List<SemanticSearchResult>();
        for (var page = 1; page <= MOST_PAGES; page++)
        {
            var result = await this.SearchAsync([dataSource], page, retrievePage);
            pages.Add(result);
            if (!result.Outcomes.Single().HasMore)
                return pages;
        }

        Assert.Fail($"The search still had more after {MOST_PAGES} pages.");
        return pages;
    }

    private async Task<SemanticSearchResult> SearchAsync(IReadOnlyList<IDataSource> dataSources, int page, Func<IDataSource, int, Task<RetrievalPage>> retrievePage)
    {
        await using var reportingScope = this.guardService.BeginAction();
        return await SemanticSearchTool.SearchAsync(new SemanticSearchRequest("mixing console", dataSources, page), retrievePage, this.guardService, reportingScope, CancellationToken.None);
    }

    /// <summary>
    /// Plays what a data source found, page by page: each page as given, and nothing after the last one.
    /// </summary>
    private static Func<IDataSource, int, Task<RetrievalPage>> Found(params IReadOnlyList<IRetrievalContext>[] retrievalPages) => (_, page) => Task.FromResult(page <= retrievalPages.Length
        ? new RetrievalPage(retrievalPages[page - 1], page < retrievalPages.Length)
        : RetrievalPage.EMPTY);

    private static IReadOnlyList<IRetrievalContext> Passages(int firstNumber, int count, int length, string dataSourceName = "Handbook") =>
        Enumerable.Range(firstNumber, count).Select(IRetrievalContext (number) => Passage(number, length, dataSourceName)).ToList();

    /// <summary>
    /// A passage whose matched text starts with its number and is as long as asked for.
    /// </summary>
    private static RetrievalTextContext Passage(int number, int length, string dataSourceName = "Handbook", int suspiciousWords = 0) => new()
    {
        DataSourceName = dataSourceName,
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_DOCUMENT,
        Path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"{dataSourceName}-{number}.md")),
        MatchedText = Text($"Passage {number:D3} ", length, suspiciousWords),
    };

    /// <summary>
    /// A text which starts as given, holds the suspicious word as often as asked for, and is as long as asked for.
    /// </summary>
    private static string Text(string start, int length, int suspiciousWords = 0)
    {
        var text = new StringBuilder(start);
        for (var word = 0; word < suspiciousWords; word++)
            text.Append(SUSPICIOUS_WORD).Append(' ');

        text.Append('x', Math.Max(0, length - text.Length));
        return text.ToString();
    }

    /// <summary>
    /// The numbers of the passages the model reads on a page, in the order it reads them.
    /// </summary>
    private static List<int> NumbersOf(SemanticSearchResult result) => PassageNumber().Matches(result.TextContent).Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();

    [GeneratedRegex(@"Passage (\d{3}) ")]
    private static partial Regex PassageNumber();
}