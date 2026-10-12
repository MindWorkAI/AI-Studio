using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how what a search found is cut into pages.
/// </summary>
/// <remarks>
/// Semantic Search lets the model page through a data source, yet nothing is kept between two
/// pages: every page is cut anew from a larger window. Three things have to hold for that. The
/// first page is what the classic RAG process always received, so the way of searching changes
/// nothing about what is found. No match turns up on two pages, although both channels of a local
/// data source often find the same chunk. And the model is told there is more whenever there might
/// be, and never that there is nothing when there is.
/// </remarks>
[TestFixture]
public sealed class RetrievalPagingTests
{
    private const int PAGE_SIZE = 2;

    [Test]
    public void TheFirstPageShowsTheFirstChannelThenWhatOnlyTheSecondFound()
    {
        // The vector search found a, b, and c; the keyword search b, d, and e:
        var (matches, _) = Merge(["a", "b", "c"], ["b", "d", "e"], page: 1);

        Assert.That(matches, Is.EqualTo(new[] { "a", "b", "d" }), "This is what the RAG process always sent: the vector matches first, then the keyword matches it did not have yet.");
    }

    [Test]
    public void EveryMatchTurnsUpOnExactlyOnePage()
    {
        string[] first = ["a", "b", "c", "d", "e", "f", "g"];
        string[] second = ["c", "h", "a", "i", "e", "j", "k"];

        var shown = new List<string>();
        for (var page = 1; page <= 3; page++)
            shown.AddRange(Merge(first, second, page).Matches);

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.Unique, "A chunk both channels found is shown on the earlier of its two pages only.");
            Assert.That(shown, Is.EquivalentTo(first.Take(6).Union(second.Take(6))), "Leaving out the duplicates must not leave out anything else.");
        });
    }

    [Test]
    public void ThereIsMoreWhenAChannelFoundMoreThanThePageHolds()
    {
        var (_, hasMore) = Merge(["a", "b", "c"], [], page: 1);

        Assert.That(hasMore, Is.True);
    }

    [Test]
    public void ThereIsNothingMoreWhenEveryChannelEndsOnThisPage()
    {
        var (_, hasMore) = Merge(["a", "b"], ["c", "d"], page: 1);

        Assert.That(hasMore, Is.False, "Neither channel found anything beyond this page, so the next one would be empty.");
    }

    [Test]
    public void TheLastPageHasNothingAfterIt()
    {
        var lastPage = RetrievalPaging.GetLastPage(PAGE_SIZE);
        var everything = Enumerable.Range(0, RetrievalPaging.MAX_RESULT_WINDOW).Select(number => $"chunk-{number}").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(Merge(everything, [], lastPage - 1).HasMore, Is.True);
            Assert.That(Merge(everything, [], lastPage).HasMore, Is.False, "No page beyond this one can be retrieved, however much the search found.");
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void APageBelowTheFirstCannotBeRetrieved(int page)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetrievalPaging.GetWindowSize(page, PAGE_SIZE));
    }

    [Test]
    public void APageBeyondTheLastCannotBeRetrieved()
    {
        var lastPage = RetrievalPaging.GetLastPage(PAGE_SIZE);

        Assert.Throws<ArgumentOutOfRangeException>(() => RetrievalPaging.GetWindowSize(lastPage + 1, PAGE_SIZE));
    }

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(10)]
    [TestCase(33)]
    [TestCase(50)]
    public void NoPageBeyondTheFirstFetchesMoreThanTheLimit(int pageSize)
    {
        var lastPage = RetrievalPaging.GetLastPage(pageSize);

        Assert.That(RetrievalPaging.GetWindowSize(lastPage, pageSize), Is.LessThanOrEqualTo(RetrievalPaging.MAX_RESULT_WINDOW), "Every page fetches its whole window again.");
    }

    [Test]
    public void TheFirstPageAlwaysHoldsTheConfiguredNumberOfMatches()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RetrievalPaging.GetLastPage(500), Is.EqualTo(1));
            Assert.That(RetrievalPaging.GetWindowSize(1, 500), Is.EqualTo(501), "The limit is for paging deeper. It must not shorten what the user asked for per search.");
        });
    }

    [Test]
    public void MatchesWithoutAKeyAreNeverTakenForOneAnother()
    {
        var (matches, _) = Merge(["", "a"], ["", "b"], page: 1);

        Assert.That(matches, Is.EqualTo(new[] { "", "a", "", "b" }));
    }

    [Test]
    public void LetterCaseDoesNotTellMatchesApart()
    {
        var (matches, _) = Merge(["CHUNK-1"], ["chunk-1", "chunk-2"], page: 1);

        Assert.That(matches, Is.EqualTo(new[] { "CHUNK-1", "chunk-2" }));
    }

    [Test]
    public void ASingleChannelIsCutInOrder()
    {
        string[] matches = ["a", "b", "c", "d", "e"];
        var secondPage = RetrievalPaging.Cut(matches, 2, PAGE_SIZE);
        var thirdPage = RetrievalPaging.Cut(matches, 3, PAGE_SIZE);

        Assert.Multiple(() =>
        {
            Assert.That(secondPage.Matches, Is.EqualTo(new[] { "c", "d" }));
            Assert.That(secondPage.HasMore, Is.True);
            Assert.That(thirdPage.Matches, Is.EqualTo(new[] { "e" }));
            Assert.That(thirdPage.HasMore, Is.False, "An ERI server which found fewer than asked for ends the paging.");
        });
    }

    private static (IReadOnlyList<string> Matches, bool HasMore) Merge(string[] first, string[] second, int page)
    {
        // A channel never returns more than it is asked for:
        var window = RetrievalPaging.GetWindowSize(page, PAGE_SIZE);
        return RetrievalPaging.Merge(first.Take(window).ToList(), second.Take(window).ToList(), match => match, page, PAGE_SIZE);
    }
}