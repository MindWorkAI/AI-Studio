using AIStudio.Tools.DocumentSegments;

namespace AIStudio.Tests.Tools.DocumentSegments;

/// <summary>
/// Checks how new words replace the segments of a document which is text itself.
/// </summary>
[TestFixture]
public sealed class TextSegmentWriterTests
{
    private const string SOURCE = "Hello world";

    [Test]
    public void SegmentsInAnyOrderAreWritten()
    {
        var segments = new[] { Segment(6, 11), Segment(0, 5) };

        var result = TextSegmentWriter.Apply(SOURCE, segments, Upper(segments), words => words, out var text);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.WRITTEN));
            Assert.That(text, Is.EqualTo("HELLO WORLD"));
        });
    }

    [Test]
    public void OverlappingSegmentsAreRefused()
    {
        var segments = new[] { Segment(0, 5), Segment(3, 8) };

        Assert.Throws<ArgumentException>(() => TextSegmentWriter.Apply(SOURCE, segments, Upper(segments), words => words, out _), "Each segment replaces its own part of the source.");
    }

    [TestCase("one\r\ntwo", ExpectedResult = "one two")]
    [TestCase("one\ntwo\rthree", ExpectedResult = "one two three")]
    [TestCase("one\u2028two\u0085three", ExpectedResult = "one two three")]
    [TestCase("no break", ExpectedResult = "no break")]
    public string NewWordsGoIntoOneLine(string words) => TextSegmentWriter.InOneLine(words);

    private static TextSegment Segment(int start, int end)
    {
        var builder = new TextSegmentBuilder();
        builder.Words(SOURCE[start..end]);
        return builder.Build(start, end);
    }

    private static SegmentResults Upper(IEnumerable<TextSegment> segments) => new(segments.ToDictionary(segment => segment.Segment.Key, segment => SegmentOutcome.Transformed(segment.Segment.Key.ToUpperInvariant())));
}