using AIStudio.Tools.DocumentSegments;

namespace AIStudio.Tests.Tools.DocumentSegments;

/// <summary>
/// Checks what the results tell about the segments of a document.
/// </summary>
[TestFixture]
public sealed class SegmentResultsTests
{
    [Test]
    public void OnlyANewTextCountsAsChanged()
    {
        var results = new SegmentResults(new Dictionary<string, SegmentOutcome>
        {
            ["Teh <1>first</1> sentence."] = SegmentOutcome.Transformed("The <1>first</1> sentence."),
            ["The second sentence."] = SegmentOutcome.Transformed("The second sentence."),
            ["A filtered sentence."] = SegmentOutcome.FILTERED,
            ["A failed sentence."] = SegmentOutcome.FAILED,
        });

        Assert.Multiple(() =>
        {
            Assert.That(results.CountOf(SegmentStatus.TRANSFORMED), Is.EqualTo(2), "A model worked on both sentences.");
            Assert.That(results.CountChanged(), Is.EqualTo(1), "A sentence returned as it was stays unchanged.");
        });
    }

    [Test]
    public void ADifferenceInCaseCountsAsChanged()
    {
        var results = new SegmentResults(new Dictionary<string, SegmentOutcome>
        {
            ["the end."] = SegmentOutcome.Transformed("The end."),
        });

        Assert.That(results.CountChanged(), Is.EqualTo(1));
    }
}