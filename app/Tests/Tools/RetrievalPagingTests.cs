using AIStudio.Settings;
using AIStudio.Settings.DataModel;
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
    public void TheChannelsTakeTurnsStartingWithTheFirst()
    {
        // The vector search found a, b, and c; the keyword search x, y, and z:
        var (matches, _) = Merge(["a", "b", "c"], ["x", "y", "z"], page: 1, pageSize: 4);

        Assert.That(matches, Is.EqualTo(new[] { "a", "x", "b", "y" }), "The best match of the vector search comes first, then the best of the keyword search, then the second best of each.");
    }

    [Test]
    public void AChannelWhichRunsOutLeavesItsTurnsToTheOther()
    {
        string[] vectorMatches = ["v1", "v2", "v3", "v4", "v5", "v6", "v7", "v8", "v9", "v10", "v11"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Merge(vectorMatches, ["k1"], page: 1, pageSize: 10).Matches, Is.EqualTo(new[] { "v1", "k1", "v2", "v3", "v4", "v5", "v6", "v7", "v8", "v9" }), "The keyword search found a single match, so the vector search fills the rest of the page.");
            Assert.That(Merge(vectorMatches, [], page: 1, pageSize: 10).Matches, Is.EqualTo(vectorMatches.Take(10)), "Without any keyword match, the vector search fills the whole page.");
        }
    }

    [Test]
    public void APageHoldsNoMoreMatchesThanConfigured()
    {
        var vectorMatches = Enumerable.Range(1, 30).Select(number => $"v{number}").ToArray();
        var keywordMatches = Enumerable.Range(1, 30).Select(number => $"k{number}").ToArray();

        Assert.That(Merge(vectorMatches, keywordMatches, page: 1, pageSize: 10).Matches, Has.Count.EqualTo(10), "When each channel brought a page of its own, a page held up to twice as many matches as the user asked for.");
    }

    [Test]
    public void AMatchBothChannelsFoundTakesTheFirstOfItsPlaces()
    {
        var (matches, _) = Merge(["a", "b", "c"], ["b", "a", "d"], page: 1, pageSize: 4);

        Assert.That(matches, Is.EqualTo(new[] { "a", "b", "c", "d" }), "Both turns of the second rank are taken already, so the third rank follows right away.");
    }

    [Test]
    public void EveryMatchTurnsUpOnExactlyOnePage()
    {
        string[] first = ["a", "b", "c", "d", "e", "f", "g"];
        string[] second = ["c", "h", "a", "i", "e", "j", "k"];

        var shown = new List<string>();
        for (var page = 1; page <= 3; page++)
            shown.AddRange(Merge(first, second, page).Matches);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shown, Is.Unique, "A chunk both channels found takes the first of its two places only.");
            Assert.That(shown, Is.EqualTo(new[] { "a", "c", "b", "h", "d", "i" }), "Leaving out the duplicates must not leave out anything else.");
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10)]
    public void TheWindowOfAPageIsEnoughToCutIt(int pageSize)
    {
        // Channels which overlap, and one of which runs out early:
        var first = Enumerable.Range(0, 60).Select(number => $"chunk-{number}").ToArray();
        var second = Enumerable.Range(0, 25).Select(number => $"chunk-{3 * number}").ToArray();

        using (Assert.EnterMultipleScope())
        {
            for (var page = 1; page <= RetrievalPaging.GetLastPage(pageSize); page++)
            {
                var fromTheWindow = Merge(first, second, page, pageSize);
                var fromEverything = RetrievalPaging.Merge(first, second, match => match, page, pageSize);

                Assert.That(fromTheWindow.Matches, Is.EqualTo(fromEverything.Matches), $"Page {page} must not depend on what lies beyond its window.");
                Assert.That(fromTheWindow.HasMore, Is.EqualTo(fromEverything.HasMore), $"Page {page} must tell the same about the next one.");
            }
        }
    }

    [Test]
    public void ThereIsMoreWhenAChannelFoundMoreThanThePageHolds()
    {
        var (_, hasMore) = Merge(["a", "b", "c"], [], page: 1);

        Assert.That(hasMore, Is.True);
    }

    [Test]
    public void ThereIsNothingMoreWhenThePageHoldsEverything()
    {
        var (_, hasMore) = Merge(["a", "b"], ["b", "a"], page: 1);

        Assert.That(hasMore, Is.False, "Both channels found the same two matches, and the page holds both, so the next one would be empty.");
    }

    [Test]
    public void WhatTheChannelsFoundBeyondThePageMakesTheNextPage()
    {
        var firstPage = Merge(["a", "b"], ["c", "d"], page: 1);
        var secondPage = Merge(["a", "b"], ["c", "d"], page: 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPage.Matches, Is.EqualTo(new[] { "a", "c" }));
            Assert.That(firstPage.HasMore, Is.True);
            Assert.That(secondPage.Matches, Is.EqualTo(new[] { "b", "d" }));
            Assert.That(secondPage.HasMore, Is.False);
        }
    }

    [Test]
    public void TheLastPageHasNothingAfterIt()
    {
        var lastPage = RetrievalPaging.GetLastPage(PAGE_SIZE);
        var everything = Enumerable.Range(0, RetrievalPaging.MAX_RESULT_WINDOW).Select(number => $"chunk-{number}").ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Merge(everything, [], lastPage - 1).HasMore, Is.True);
            Assert.That(Merge(everything, [], lastPage).HasMore, Is.False, "No page beyond this one can be retrieved, however much the search found.");
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RetrievalPaging.GetLastPage(500), Is.EqualTo(1));
            Assert.That(RetrievalPaging.GetWindowSize(1, 500), Is.EqualTo(501), "The limit is for paging deeper. It must not shorten what the user asked for per search.");
        }
    }

    [Test]
    public void MatchesWithoutAKeyAreNeverTakenForOneAnother()
    {
        var (matches, _) = Merge(["", "a"], ["", "b"], page: 1, pageSize: 4);

        Assert.That(matches, Is.EqualTo(new[] { "", "", "a", "b" }));
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondPage.Matches, Is.EqualTo(new[] { "c", "d" }));
            Assert.That(secondPage.HasMore, Is.True);
            Assert.That(thirdPage.Matches, Is.EqualTo(new[] { "e" }));
            Assert.That(thirdPage.HasMore, Is.False, "A channel which found fewer than asked for ends the paging.");
        }
    }

    [TestCase((ushort)1)]
    [TestCase((ushort)10)]
    [TestCase((ushort)500)]
    public void AnERIDataSourceHasASinglePage(ushort maxMatches)
    {
        IDataSource source = new DataSourceERI_V1 { Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = maxMatches };

        Assert.That(source.GetLastPage(), Is.EqualTo(1), "ERI v1 knows no offset, and a server which allows only the configured number of matches would refuse every further page.");
    }

    [Test]
    public void ALocalDataSourcePagesAsItsPageSizeAllows()
    {
        IDataSource source = new DataSourceLocalDirectory { Id = "11111111-1111-1111-1111-111111111111", Name = "Handbook", MaxMatches = 10 };

        Assert.That(source.GetLastPage(), Is.EqualTo(RetrievalPaging.GetLastPage(10)));
    }

    [Test]
    public async Task AnERIDataSourceRefusesAFurtherPage()
    {
        var source = new DataSourceERI_V1 { Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = 10 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await source.RetrieveDataAsync("query", 2, new AIStudio.Chat.ChatThread()));
    }

    private static (IReadOnlyList<string> Matches, bool HasMore) Merge(string[] first, string[] second, int page, int pageSize = PAGE_SIZE)
    {
        // A channel never returns more than it is asked for:
        var window = RetrievalPaging.GetWindowSize(page, pageSize);
        return RetrievalPaging.Merge(first.Take(window).ToList(), second.Take(window).ToList(), match => match, page, pageSize);
    }
}