using AIStudio.Tools.DocumentSegments;

namespace AIStudio.Tests.Tools.DocumentSegments;

/// <summary>
/// Checks how the segments of a document are grouped into the requests to a model.
/// </summary>
[TestFixture]
public sealed class SegmentBatchPlannerTests
{
    [Test]
    public void EveryTextGoesOnceInTheOrderOfTheDocument()
    {
        var segments = Segments("Introduction", "Page 1", "2026", "Introduction", "Summary", "Page 1");

        var batches = SegmentBatchPlanner.Plan(segments);

        Assert.That(batches.Single().Select(segment => segment.Key), Is.EqualTo(new[] { "Introduction", "Page 1", "Summary" }), "A repeated text goes once, and a text without letters stays out.");
    }

    [Test]
    public void ABatchEndsBeforeItGetsTooLong()
    {
        var segments = Segments("aaaa", "bbbb", "cccc", "dddd", "eeee");

        var batches = SegmentBatchPlanner.Plan(segments, maxCharacters: 10, maxSegments: 40);

        Assert.That(batches.Select(batch => batch.Count), Is.EqualTo(new[] { 2, 2, 1 }));
    }

    [Test]
    public void ABatchEndsAfterItsLastSegment()
    {
        var segments = Segments("a", "b", "c", "d", "e");

        var batches = SegmentBatchPlanner.Plan(segments, maxCharacters: 1_000, maxSegments: 2);

        Assert.That(batches.Select(batch => batch.Count), Is.EqualTo(new[] { 2, 2, 1 }));
    }

    [Test]
    public void ASegmentLongerThanABatchGetsABatchOfItsOwn()
    {
        var segments = Segments("short", new string('x', 50), "end");

        var batches = SegmentBatchPlanner.Plan(segments, maxCharacters: 20, maxSegments: 40);

        Assert.That(batches.Select(batch => batch.Count), Is.EqualTo(new[] { 1, 1, 1 }));
    }

    [Test]
    public void ADocumentWithoutWordsNeedsNoRequest()
    {
        Assert.That(SegmentBatchPlanner.Plan(Segments("1", "2026-11-09", "—")), Is.Empty);
    }

    private static IEnumerable<DocumentSegment> Segments(params string[] texts) => texts.Select(text => new DocumentSegment(new TaggedTextBuilder().Words(text).Build()));
}