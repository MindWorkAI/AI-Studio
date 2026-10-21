using AIStudio.Components;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks how much of a long text the content viewer shows.
/// </summary>
/// <remarks>
/// The viewer shows only the beginning of a text which is too long to render, and it must not tear
/// a line apart while doing so: a cut through a table row or a code fence would show the user a
/// broken document, as if we had read it wrong.
/// </remarks>
[TestFixture]
public sealed class ContentViewerTests
{
    [Test]
    public void ATextWhichFitsIsShownEntirely()
    {
        const string TEXT = "# Heading\nA short passage.";

        var shown = ContentViewer.CutAtLineBreak(TEXT, 100, out var cutOffCharacters);

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.SameAs(TEXT));
            Assert.That(cutOffCharacters, Is.Zero);
        });
    }

    [Test]
    public void ATextOfExactlyTheLimitIsShownEntirely()
    {
        var text = new string('a', 50);

        var shown = ContentViewer.CutAtLineBreak(text, 50, out var cutOffCharacters);

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo(text));
            Assert.That(cutOffCharacters, Is.Zero);
        });
    }

    [Test]
    public void ALongTextIsCutAfterTheLastLineBreakBeforeTheLimit()
    {
        const string TEXT = "| a | b |\n| 1 | 2 |\n| 3 | 4 |";

        var shown = ContentViewer.CutAtLineBreak(TEXT, 25, out var cutOffCharacters);

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo("| a | b |\n| 1 | 2 |\n"), "The last table row would be torn apart, so it goes as a whole.");
            Assert.That(cutOffCharacters, Is.EqualTo(TEXT.Length - shown.Length));
        });
    }

    [Test]
    public void ALongTextWithoutLineBreaksIsCutAtTheLimit()
    {
        var text = new string('a', 30);

        var shown = ContentViewer.CutAtLineBreak(text, 20, out var cutOffCharacters);

        Assert.Multiple(() =>
        {
            Assert.That(shown, Has.Length.EqualTo(20));
            Assert.That(cutOffCharacters, Is.EqualTo(10));
        });
    }
}