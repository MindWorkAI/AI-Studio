using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the notes the BeyondFacts Lab shows before the first use, and when they show again.
/// </summary>
[TestFixture]
public sealed class BflNoticeTests
{
    [Test]
    public async Task TheNotesComeInBothLanguages()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await BflNotice.ReadAsync("de-DE"), Does.Contain("Prototyp"));
            Assert.That(await BflNotice.ReadAsync("en-US"), Does.Contain("prototype"));
        }
    }

    [TestCase("de-DE", BflNotice.GERMAN_PATH)]
    [TestCase("de-AT", BflNotice.GERMAN_PATH)]
    [TestCase("en-US", BflNotice.ENGLISH_PATH)]
    [TestCase("fr-FR", BflNotice.ENGLISH_PATH)]
    public void EveryLanguageButGermanGetsTheEnglishNotes(string ietfTag, string expectedPath)
    {
        Assert.That(BflNotice.PathFor(ietfTag), Is.EqualTo(expectedPath));
    }

    [Test]
    public void AChangeOfEitherLanguageChangesTheHash()
    {
        var hash = BflNotice.ComputeHash("Hinweis", "Note");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflNotice.ComputeHash("Hinweis, geändert", "Note"), Is.Not.EqualTo(hash));
            Assert.That(BflNotice.ComputeHash("Hinweis", "Note, changed"), Is.Not.EqualTo(hash));
            Assert.That(BflNotice.ComputeHash("Note", "Hinweis"), Is.Not.EqualTo(hash));
        }
    }

    [Test]
    public void LineEndingsAndOuterSpaceDoNotChangeTheHash()
    {
        var hash = BflNotice.ComputeHash("Erste Zeile\nZweite Zeile", "First line\nSecond line");

        Assert.That(BflNotice.ComputeHash("Erste Zeile\r\nZweite Zeile\r\n", "  First line\r\nSecond line"), Is.EqualTo(hash));
    }

    [Test]
    public async Task OnlyTheHashOfTheNotesAsTheyAreNowCountsAsConfirmed()
    {
        var currentHash = await BflNotice.ComputeHashAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(currentHash, Has.Length.EqualTo(64));
            Assert.That(BflNotice.IsAccepted(currentHash, currentHash), Is.True);
            Assert.That(BflNotice.IsAccepted(currentHash.ToUpperInvariant(), currentHash), Is.True);
            Assert.That(BflNotice.IsAccepted(string.Empty, currentHash), Is.False, "Nobody confirmed anything yet.");
            Assert.That(BflNotice.IsAccepted(BflNotice.ComputeHash("old", "old"), currentHash), Is.False, "The notes changed since.");
        }
    }
}