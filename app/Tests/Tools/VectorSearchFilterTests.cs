using AIStudio.Tools.Databases.VectorStore;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how a vector search is restricted to some of the points in a store.
/// </summary>
/// <remarks>
/// The restriction comes from other conditions of a search, e.g., a sender or a date range. When it
/// leaks, the answer contains exactly what the user ruled out, so these tests hold on to the cases in
/// which that would happen quietly: an empty restriction, a restriction too long to be sent along,
/// and a vector store which returns more than it was asked for.
/// </remarks>
[TestFixture]
public sealed class VectorSearchFilterTests
{
    private const string POINT_X = "0b5f1e8a-3c2d-4e6f-9a1b-7c8d9e0f1a2b";
    private const string POINT_Y = "1c6a2f9b-4d3e-4f70-8b2c-8d9e0f1a2b3c";
    private const string POINT_Z = "2d7b3a0c-5e4f-4a81-9c3d-9e0f1a2b3c4d";

    [Test]
    public void AFilterWithoutAnyPointMatchesNothing()
    {
        var filter = new VectorSearchFilter([]);

        Assert.Multiple(() =>
        {
            Assert.That(filter.MatchesNothing, Is.True, "Conditions which matched nothing must not turn into a search of the whole store.");
            Assert.That(filter.Apply([Match(POINT_X)], 10), Is.Empty);
        });
    }

    [Test]
    public void AShortFilterTravelsWithTheSearch()
    {
        var filter = new VectorSearchFilter([POINT_X, POINT_Y]);

        Assert.Multiple(() =>
        {
            Assert.That(filter.MatchesNothing, Is.False);
            Assert.That(filter.GetRequestPointIds(), Is.EquivalentTo(new[] { POINT_X, POINT_Y }));
            Assert.That(filter.GetCandidateCount(10), Is.EqualTo(10), "The vector store already looks at these points only, so asking for more would buy nothing.");
        });
    }

    [Test]
    public void AFilterLongerThanOneRequestIsAppliedAfterALargerSearch()
    {
        var longest = new VectorSearchFilter(PointIds(VectorSearchFilter.MAX_POINT_IDS_PER_REQUEST));
        var tooLong = new VectorSearchFilter(PointIds(VectorSearchFilter.MAX_POINT_IDS_PER_REQUEST + 1));

        Assert.Multiple(() =>
        {
            Assert.That(longest.GetRequestPointIds(), Has.Count.EqualTo(VectorSearchFilter.MAX_POINT_IDS_PER_REQUEST));
            Assert.That(tooLong.GetRequestPointIds(), Is.Null);
            Assert.That(tooLong.GetCandidateCount(10), Is.EqualTo(10 * VectorSearchFilter.OVERSAMPLING_FACTOR), "Some of the matches will be dropped afterward, so the search has to ask for more.");
            Assert.That(tooLong.GetCandidateCount(int.MaxValue), Is.EqualTo(int.MaxValue));
        });
    }

    [Test]
    public void OnlyMatchesInsideTheFilterAreKeptInTheirOrder()
    {
        var filter = new VectorSearchFilter([POINT_Y.ToUpperInvariant(), POINT_Z]);
        var candidates = new[] { Match(POINT_X), Match(POINT_Z), Match(POINT_Y) };

        Assert.Multiple(() =>
        {
            Assert.That(filter.Apply(candidates, 10).Select(match => match.PointId), Is.EqualTo(new[] { POINT_Z, POINT_Y }), "The closest match lies outside the filter, whatever the vector store returned.");
            Assert.That(filter.Apply(candidates, 1).Select(match => match.PointId), Is.EqualTo(new[] { POINT_Z }));
        });
    }

    [Test]
    public void AFilterWithAMalformedPointIdIsRejected()
    {
        Assert.Throws<FormatException>(() => _ = new VectorSearchFilter([POINT_X, "not-a-point-id"]), "Skipping the malformed id would quietly search fewer points than asked for.");
    }

    private static IEnumerable<string> PointIds(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid().ToString());

    private static VectorSearchResult Match(string pointId) => new(
        pointId,
        0.5,
        "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11",
        "LOCAL_DIRECTORY",
        pointId,
        string.Empty,
        "/tmp/test-data/notes.md",
        "/tmp/test-data/notes.md",
        "notes.md",
        "notes.md",
        "md",
        null,
        0,
        "Some text.",
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);
}