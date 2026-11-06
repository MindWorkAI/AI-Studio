using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how a search splits what the user typed, and how it shows the user what it found.
/// </summary>
[TestFixture]
public sealed class SearchTextTests
{
    private static IEnumerable<string> MarkedParts(string text, params string[] terms) => SearchText.Highlight(text, terms)
        .Where(part => part.IsMatch)
        .Select(part => part.Text);

    [Test]
    public void EveryWordCountsOnceWhateverItsCase()
    {
        Assert.That(SearchText.ParseTerms("  Budget  alpha budget "), Is.EqualTo(new[] { "Budget", "alpha" }));
    }

    [Test]
    public void WordsInQuotesFormOnePhrase()
    {
        Assert.That(SearchText.ParseTerms("\"test case\" budget"), Is.EqualTo(new[] { "test case", "budget" }));
    }

    [Test]
    public void TypographicQuotesFormAPhraseToo()
    {
        Assert.That(SearchText.ParseTerms("„test case“ budget"), Is.EqualTo(new[] { "test case", "budget" }));
    }

    [Test]
    public void AQuoteWithoutItsClosingOneLeavesTheWordsOnTheirOwn()
    {
        Assert.That(SearchText.ParseTerms("budget \"test case"), Is.EqualTo(new[] { "budget", "test", "case" }));
    }

    [Test]
    public void EmptyQuotesCountForNothing()
    {
        Assert.That(SearchText.ParseTerms("\"\" budget \"  \""), Is.EqualTo(new[] { "budget" }));
    }

    [Test]
    public void SpacesWithinAPhraseCountAsOne()
    {
        Assert.That(SearchText.ParseTerms("\" test    case \""), Is.EqualTo(new[] { "test case" }));
    }

    [Test]
    public void APhraseIsMissingWhenItsWordsAreOnlyApart()
    {
        Assert.That(SearchText.MissingTerms("The case for the test", ["test case"]), Is.EqualTo(new[] { "test case" }));
    }

    [Test]
    public void APhraseIsMarkedAsOnePart()
    {
        Assert.That(MarkedParts("A new test case here", "test case"), Is.EqualTo(new[] { "test case" }));
    }

    [Test]
    public void AWordIsMissingOnlyWhenTheTextLacksItInAnyCase()
    {
        Assert.That(SearchText.MissingTerms("Project Alpha", ["alpha", "budget"]), Is.EqualTo(new[] { "budget" }));
    }

    [Test]
    public void EveryOccurrenceIsMarkedInItsOwnCase()
    {
        Assert.That(MarkedParts("Budget and budget", "BUDGET"), Is.EqualTo(new[] { "Budget", "budget" }));
    }

    [Test]
    public void TheTextBetweenTheWordsStaysUnmarked()
    {
        var parts = SearchText.Highlight("Q3 budget draft", ["budget"]);
        Assert.That(parts, Is.EqualTo(new SearchTextPart[] { new("Q3 ", false), new("budget", true), new(" draft", false) }));
    }

    [Test]
    public void OverlappingWordsFormOnePart()
    {
        Assert.That(MarkedParts("forecast", "fore", "recast"), Is.EqualTo(new[] { "forecast" }));
    }

    [Test]
    public void ATextWithoutTheWordsIsOneUnmarkedPart()
    {
        Assert.That(SearchText.Highlight("Kickoff notes", ["budget"]), Is.EqualTo(new SearchTextPart[] { new("Kickoff notes", false) }));
    }

    [Test]
    public void APassageStartsShortlyBeforeTheWordWithAWholeWord()
    {
        var message = "In the kickoff meeting last Tuesday, we fixed the budget at 40k for the first phase.";

        var passage = SearchText.FindPassage(message, ["budget"], charactersBefore: 20);

        Assert.That(passage, Does.StartWith("… we fixed the budget at 40k"));
    }

    [Test]
    public void APassageIsCutWithAnEllipsis()
    {
        var message = "The budget " + string.Join(' ', Enumerable.Repeat("word", 100));

        var passage = SearchText.FindPassage(message, ["budget"], maximumLength: 30);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(passage, Does.StartWith("The budget"));
            Assert.That(passage, Does.EndWith(" …"));
        }
    }

    [Test]
    public void APassageShowsMarkdownAsTheUserReadsIt()
    {
        var message = "# Plan\n\nWe fixed the **budget**, see [the sheet](https://example.com/sheet).\n- `40k` first";

        var passage = SearchText.FindPassage(message, ["budget"]);

        Assert.That(passage, Is.EqualTo("Plan We fixed the budget, see the sheet. 40k first"));
    }

    [Test]
    public void AWordOnlyInALinkAddressLeavesThePassageAtTheBeginning()
    {
        var message = "See [the sheet](https://example.com/budget) for the numbers.";

        var passage = SearchText.FindPassage(message, ["budget"]);

        Assert.That(passage, Is.EqualTo("See the sheet for the numbers."));
    }
}