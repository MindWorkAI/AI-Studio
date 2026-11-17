using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how results are cut into pages by their length.
/// </summary>
/// <remarks>
/// A search which returns only what fits into its budget must not lose the rest: what does not fit
/// on one page has to start the next one. Skipping a long result to make room for shorter ones
/// after it would leave a gap in the middle of the ranking, and since a page is cut anew every
/// time, no later page would ever bring the skipped result back.
/// </remarks>
[TestFixture]
public sealed class CharacterPagingTests
{
    private const int BUDGET = 60;

    [Test]
    public void ResultsWhichFitTogetherStayOnOnePage()
    {
        Assert.That(CharacterPaging.Cut([10, 20, 30], BUDGET), Is.EqualTo(new CharacterPage[] { new(0, 3) }));
    }

    [Test]
    public void APageEndsBeforeTheFirstResultWhichNoLongerFits()
    {
        var pages = CharacterPaging.Cut([40, 30, 20, 10], BUDGET);

        Assert.That(pages, Is.EqualTo(new CharacterPage[] { new(0, 1), new(1, 3) }), "The second result starts the next page, instead of the third one taking its place on the first.");
    }

    [Test]
    public void AResultWhichFillsTheRestExactlyStillFits()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(CharacterPaging.Cut([BUDGET], BUDGET), Is.EqualTo(new CharacterPage[] { new(0, 1) }));
            Assert.That(CharacterPaging.Cut([30, 30, 1], BUDGET), Is.EqualTo(new CharacterPage[] { new(0, 2), new(2, 1) }));
        }
    }

    [Test]
    public void AResultLongerThanTheBudgetFillsAPageOfItsOwn()
    {
        var pages = CharacterPaging.Cut([10, 100, 10], BUDGET);

        Assert.That(pages, Is.EqualTo(new CharacterPage[] { new(0, 1), new(1, 1), new(2, 1) }), "It must neither be lost nor hold up the results after it.");
    }

    [Test]
    public void EveryResultTurnsUpOnExactlyOnePageInOrder()
    {
        var random = new Random(4711);
        var lengths = Enumerable.Range(0, 200).Select(_ => random.Next(0, 2 * BUDGET)).ToArray();

        var pages = CharacterPaging.Cut(lengths, BUDGET);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages.SelectMany(page => Enumerable.Range(page.Start, page.Count)), Is.EqualTo(Enumerable.Range(0, lengths.Length)));
            Assert.That(pages, Has.All.Matches<CharacterPage>(page => page.Count >= 1));
            Assert.That(pages.Where(page => page.Count > 1), Has.All.Matches<CharacterPage>(page => lengths[page.Start..page.End].Sum() <= BUDGET), "Only a result alone on its page may exceed the budget.");
            Assert.That(pages.Zip(pages.Skip(1)), Has.All.Matches<(CharacterPage Page, CharacterPage Next)>(pair => lengths[pair.Page.Start..pair.Page.End].Sum() + lengths[pair.Next.Start] > BUDGET), "A page ends only where the next result no longer fits.");
        }
    }

    [Test]
    public void NoResultsMakeNoPages()
    {
        Assert.That(CharacterPaging.Cut([], BUDGET), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ABudgetBelowOneIsRefused(int budget)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CharacterPaging.Cut([10], budget));
    }

    [Test]
    public void ANegativeLengthIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CharacterPaging.Cut([10, -1], BUDGET));
    }
}