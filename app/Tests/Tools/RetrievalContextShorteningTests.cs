using AIStudio.Settings;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Security;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how a passage too long for any page is shortened.
/// </summary>
/// <remarks>
/// A passage which does not fit even on its own would hold up every passage after it, so it is
/// shortened instead. The cut should keep a line whole where that costs little, and it must never
/// break a character in two.
/// </remarks>
[TestFixture]
public sealed class RetrievalContextShorteningTests
{
    [Test]
    public void ATextWhichFitsStaysAsItIs()
    {
        const string TEXT = "first line\nsecond line";

        Assert.That(RetrievalContextExtensions.CutAtLineEnd(TEXT, TEXT.Length), Is.SameAs(TEXT));
    }

    [Test]
    public void ACutEndsAtTheLastLineEndWhichFits()
    {
        Assert.That(RetrievalContextExtensions.CutAtLineEnd("first\nsecond\nthird", 15), Is.EqualTo("first\nsecond"));
    }

    [Test]
    public void ACutKeepsNoHalfOfAWindowsLineEnd()
    {
        Assert.That(RetrievalContextExtensions.CutAtLineEnd("first\r\nsecond\r\nthird", 17), Is.EqualTo("first\r\nsecond"));
    }

    [Test]
    public void ACutGivesUpAtMostHalfOfWhatFitsForALineEnd()
    {
        Assert.That(RetrievalContextExtensions.CutAtLineEnd("a\nbcdefghijkl", 10), Is.EqualTo("a\nbcdefghi"), "With its only line end right at the start, the text would lose nearly everything.");
    }

    [Test]
    public void ACutNeverSplitsASurrogatePair()
    {
        Assert.That(RetrievalContextExtensions.CutAtLineEnd("ab😀cd", 3), Is.EqualTo("ab"));
    }

    [Test]
    public void NothingIsLeftOfATextWithoutRoom()
    {
        Assert.That(RetrievalContextExtensions.CutAtLineEnd("text", 0), Is.Empty);
    }

    [Test]
    public async Task AContextWhichFitsIsNeitherShortenedNorFilteredAgain()
    {
        var context = new RetrievalTextContext { DataSourceName = "Handbook", Category = RetrievalContentCategory.TEXT, Type = RetrievalContentType.TEXT_DOCUMENT, Path = "handbook.md", MatchedText = "The mixing console is described here." };
        var prepared = new PreparedRetrievalContext(context, "The mixing console is described here.", [], null);

        // Without a runtime, filtering again would fail:
        var guardService = new PromptInjectionGuardService(null!, new SettingsManager(NullLogger<SettingsManager>.Instance, null!), NullLogger<PromptInjectionGuardService>.Instance, NullLoggerFactory.Instance);
        var shortened = await prepared.ShortenToFitAsync(prepared.Body.Length, preparedContext => preparedContext.Body.Length, guardService);

        Assert.That(shortened, Is.SameAs(prepared));
    }
}