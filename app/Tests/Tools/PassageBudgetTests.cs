using System.Text;

using AIStudio.Models;
using AIStudio.Settings;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which passages of the classic RAG process fit into its budget.
/// </summary>
/// <remarks>
/// Most tests count in a unit of their own, as tokens are one: only the filler of the made-up
/// passages counts, so that the descriptions and headers which AI Studio writes around them do not
/// get into the way of the numbers. FakeRuntime plays the filter for prompt injections.
/// </remarks>
[TestFixture]
public sealed class PassageBudgetTests
{
    /// <summary>
    /// What the filter removes.
    /// </summary>
    private const string SUSPICIOUS_WORD = "PWNED";

    /// <summary>
    /// What a made-up passage is filled with, and what the budgets of most tests count.
    /// </summary>
    private const char FILLER = '•';

    /// <summary>
    /// A budget in characters which holds a short passage along with its description, but not a long one.
    /// </summary>
    private const int CHARACTERS = 5_000;

    /// <summary>
    /// A passage of this length fits easily into a budget of CHARACTERS.
    /// </summary>
    private const int SHORT = 100;

    /// <summary>
    /// A passage of this length exceeds a budget of CHARACTERS on its own.
    /// </summary>
    private const int OVERLONG = 20_000;

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
    public async Task AKnownContextWindowLeavesHalfOfItToThePassagesCountedInTokens()
    {
        var countedTexts = new List<string>();
        var budget = PassageBudget.For(ContextWindow.Of(10_000), (text, _) =>
        {
            countedTexts.Add(text);
            return Task.FromResult(1);
        });

        var selection = await budget.SelectAsync([await this.PrepareAsync(Passage("Handbook", 1, SHORT))], this.guardService);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(budget.Limit, Is.EqualTo(5_000));
            Assert.That(countedTexts.SingleOrDefault(), Does.Contain("Passage 001 "), "The tokens of the passage are counted, as the model counts them.");
            Assert.That(selection.Contexts, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task WithoutAKnownContextWindowThePassagesGetAsManyCharactersAsAPageOfSemanticSearch()
    {
        var budget = PassageBudget.For(ContextWindow.UNKNOWN, (_, _) => throw new AssertionException("Without a known window, nothing is counted in tokens."));

        var selection = await budget.SelectAsync([await this.PrepareAsync(Passage("Handbook", 1, SHORT))], this.guardService);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(budget.Limit, Is.EqualTo(SemanticSearchTool.MAX_RESULT_CHARACTERS));
            Assert.That(selection.Contexts, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task DataSourcesTakeTurnsByRankAndStayGroupedByDataSource()
    {
        var handbook = await this.PrepareAsync(Passage("Handbook", 1, 100), Passage("Handbook", 2, 100), Passage("Handbook", 3, 100));
        var minutes = await this.PrepareAsync(Passage("Minutes", 1, 100), Passage("Minutes", 2, 100), Passage("Minutes", 3, 100));

        var selection = await FillerBudget(300).SelectAsync([handbook, minutes], this.guardService);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.Contexts, Is.EqualTo(new[] { handbook[0], handbook[1], minutes[0] }), "The best passage of each data source comes before the second best of any; the model reads them by data source.");
            Assert.That(selection.PassageCounts, Is.EqualTo(new[] { 2, 1 }));
            Assert.That(selection.LeftOutCount, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task APassageWhichDoesNotFitIsSkippedAndAShorterOneStillComesIn()
    {
        var handbook = await this.PrepareAsync(Passage("Handbook", 1, 200), Passage("Handbook", 2, 150), Passage("Handbook", 3, 50));

        var selection = await FillerBudget(250).SelectAsync([handbook], this.guardService);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.Contexts, Is.EqualTo(new[] { handbook[0], handbook[2] }));
            Assert.That(selection.LeftOutCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task EveryPassageIsEitherSelectedOrCountedAsLeftOut()
    {
        var random = new Random(4711);
        var contextsByDataSource = new List<IReadOnlyList<PreparedRetrievalContext>>();
        foreach (var dataSourceName in new[] { "Handbook", "Minutes", "Intranet" })
            contextsByDataSource.Add(await this.PrepareAsync(Enumerable.Range(1, random.Next(0, 12)).Select(number => Passage(dataSourceName, number, random.Next(1, 400))).ToArray()));

        var selection = await FillerBudget(1_000).SelectAsync(contextsByDataSource, this.guardService);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.Contexts.Count + selection.LeftOutCount, Is.EqualTo(contextsByDataSource.Sum(contexts => contexts.Count)));
            Assert.That(selection.Contexts.Sum(context => context.Body.Count(character => character == FILLER)), Is.LessThanOrEqualTo(1_000));
            Assert.That(selection.Contexts, Is.EqualTo(contextsByDataSource.SelectMany(contexts => contexts).Where(selection.Contexts.Contains)), "The model reads the passages by data source, each in the order of their rank.");
        }
    }

    [Test]
    public async Task NothingToSelectGivesAnEmptySelection()
    {
        var withoutDataSources = await FillerBudget(1_000).SelectAsync([], this.guardService);
        var withoutPassages = await FillerBudget(1_000).SelectAsync([[], []], this.guardService);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withoutDataSources.Contexts, Is.Empty);
            Assert.That(withoutDataSources.LeftOutCount, Is.Zero);
            Assert.That(withoutPassages.Contexts, Is.Empty);
            Assert.That(withoutPassages.PassageCounts, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(withoutPassages.LeftOutCount, Is.Zero);
        }
    }

    [Test]
    public async Task APassageLongerThanTheWholeBudgetIsShortenedAndFilteredAgain()
    {
        var handbook = await this.PrepareAsync(Passage("Handbook", 1, OVERLONG, suspiciousWords: 1));
        var filteredBefore = this.runtime.FilteredBatches.Count;

        var selection = await PassageBudget.InCharacters(CHARACTERS).SelectAsync([handbook], this.guardService);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.Contexts, Has.Count.EqualTo(1), "A passage too long for the whole budget is shortened instead of left out.");
            Assert.That(selection.LeftOutCount, Is.Zero);
            Assert.That(selection.Contexts[0].ToMarkdown(1, 1), Has.Length.LessThanOrEqualTo(CHARACTERS));
            Assert.That(selection.Contexts[0].Body, Does.Contain("Matched text content, shortened to fit the size limit:"), "The label tells the model that the text is shorter than what the data source found.");
            Assert.That(selection.Contexts[0].Body, Does.Not.Contain(SUSPICIOUS_WORD).And.Contain(FakeRuntime.REDACTION_MARKER));
            Assert.That(this.runtime.FilteredBatches, Has.Count.GreaterThan(filteredBefore), "The shorter passage passes the filter again.");
            Assert.That(this.runtime.FilteredBatches[^1].SingleOrDefault(text => text.StartsWith("Passage 001 ", StringComparison.Ordinal)), Has.Length.LessThan(OVERLONG), "The filter saw the shorter text, not the one the data source found.");
        }
    }

    [Test]
    public async Task APassageIsShortenedUntilItFitsAlsoWhenTheBudgetDoesNotCountCharacters()
    {
        var handbook = await this.PrepareAsync(Passage("Handbook", 1, OVERLONG));

        // Like a tokenizer, which counts several characters as one token, but not always the same number:
        var budget = new PassageBudget(1_000, (text, _) => Task.FromResult(text.Length / 3 + text.Count(character => character == FILLER) / 7));
        var selection = await budget.SelectAsync([handbook], this.guardService);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.Contexts, Has.Count.EqualTo(1));
            var markdown = selection.Contexts[0].ToMarkdown(1, 1);
            Assert.That(markdown.Length / 3 + markdown.Count(character => character == FILLER) / 7, Is.LessThanOrEqualTo(1_000));
        }
    }

    [Test]
    public async Task AShortenedPassageStillHasToFitIntoWhatIsLeft()
    {
        var handbook = await this.PrepareAsync(Passage("Handbook", 1, SHORT));
        var minutes = await this.PrepareAsync(Passage("Minutes", 1, OVERLONG));

        var selection = await PassageBudget.InCharacters(CHARACTERS).SelectAsync([handbook, minutes], this.guardService);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.Contexts, Is.EqualTo(new[] { handbook[0] }), "The shortened passage fills a whole budget, of which the first passage took a part already.");
            Assert.That(selection.PassageCounts, Is.EqualTo(new[] { 1, 0 }), "A data source none of whose passages fit counts none.");
            Assert.That(selection.LeftOutCount, Is.EqualTo(1));
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ABudgetBelowOneIsRefused(int limit) => Assert.Throws<ArgumentOutOfRangeException>(() => PassageBudget.InCharacters(limit));

    /// <summary>
    /// A budget which counts the filler of the made-up passages only.
    /// </summary>
    private static PassageBudget FillerBudget(int limit) => new(limit, (text, _) => Task.FromResult(text.Count(character => character == FILLER)));

    /// <summary>
    /// Prepares made-up passages of one data source for the model, in the order given.
    /// </summary>
    private async Task<IReadOnlyList<PreparedRetrievalContext>> PrepareAsync(params IReadOnlyList<IRetrievalContext> contexts) => await contexts.PrepareForModelAsync(this.guardService);

    /// <summary>
    /// A made-up passage whose matched text holds as much filler as asked for, after the suspicious word as often as asked for.
    /// </summary>
    private static RetrievalTextContext Passage(string dataSourceName, int number, int fillerLength, int suspiciousWords = 0)
    {
        var text = new StringBuilder($"Passage {number:D3} ");
        for (var word = 0; word < suspiciousWords; word++)
            text.Append(SUSPICIOUS_WORD).Append(' ');

        text.Append(FILLER, fillerLength);
        return new()
        {
            DataSourceName = dataSourceName,
            Category = RetrievalContentCategory.TEXT,
            Type = RetrievalContentType.TEXT_DOCUMENT,
            Path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"{dataSourceName}-{number}.md")),
            MatchedText = text.ToString(),
        };
    }
}