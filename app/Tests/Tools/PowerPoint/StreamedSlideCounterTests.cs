using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks how the slides of an answer are counted while it streams in.
/// </summary>
[TestFixture]
public sealed class StreamedSlideCounterTests
{
    private const string ANSWER = """{"title": "Deck", "slides": [{"layout": "L1", "title": "Start"}, {"template_slide": 2}, {"layout": "L3", "title": "Results", "texts": [["All \"layout\" keys"]]}]}""";

    [Test]
    public void SlidesOnLayoutsAndTemplateSlidesAreCounted()
    {
        Assert.That(new StreamedSlideCounter().Count(ANSWER), Is.EqualTo(3), "An escaped key within a text does not start a slide.");
    }

    [Test]
    public void EverySplitOfTheStreamGivesTheSameCount()
    {
        //
        // Wherever the chunks end, a key cut in two by them is counted once, and only once
        // it is complete:
        //
        for (var chunkLength = 1; chunkLength <= 20; chunkLength++)
        {
            var counter = new StreamedSlideCounter();
            var count = 0;
            for (var end = chunkLength; end < ANSWER.Length + chunkLength; end += chunkLength)
                count = counter.Count(ANSWER[..Math.Min(end, ANSWER.Length)]);

            Assert.That(count, Is.EqualTo(3), $"Chunks of {chunkLength} characters");
        }
    }

    [Test]
    public void AKeyCountsOnlyOnceItIsComplete()
    {
        var counter = new StreamedSlideCounter();
        Assert.Multiple(() =>
        {
            Assert.That(counter.Count("""{"slides": [{"lay"""), Is.Zero);
            Assert.That(counter.Count("""{"slides": [{"layout"""), Is.Zero, "The closing quotation mark is still missing.");
            Assert.That(counter.Count("""{"slides": [{"layout": "L1"}"""), Is.EqualTo(1));
        });
    }

    [Test]
    public void AShorterAnswerIsCountedAnew()
    {
        var counter = new StreamedSlideCounter();
        counter.Count(ANSWER);

        Assert.That(counter.Count("""{"slides": [{"layout": "L1"}"""), Is.EqualTo(1));
    }
}
