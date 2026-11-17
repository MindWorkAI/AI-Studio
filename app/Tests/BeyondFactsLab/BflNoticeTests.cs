using System.Runtime.CompilerServices;

using AIStudio.Assistants.BeyondFactsLab;

using SharedTools;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the notes the BeyondFacts Lab shows before the first use, and when they show again.
/// </summary>
[TestFixture]
public sealed class BflNoticeTests
{
    [Test]
    public void TheNotesAreMarkdownWithAHeadingAndParagraphs()
    {
        var paragraphs = BflNotice.Text.ReplaceLineEndings("\n").Split("\n\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paragraphs[0], Is.EqualTo("## Welcome!"));
            Assert.That(paragraphs, Has.Length.EqualTo(5));
            Assert.That(paragraphs[^1], Does.Contain("prototype"));
            Assert.That(paragraphs, Has.None.StartsWith(" "), "The indentation of the source must not reach the notes.");
        }
    }

    [Test]
    public void TheCollectorFindsTheNotesAsTheAppShowsThem()
    {
        var source = File.ReadAllText(NoticeSourceFile());
        var warnings = new List<string>();

        var texts = I18NSourceScanner.FindTexts(source, NoticeSourceFile(), warnings).Select(text => I18NKeyHash.NormalizeLineEndings(text.Text));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(texts, Does.Contain(I18NKeyHash.NormalizeLineEndings(BflNotice.Text)));
            Assert.That(warnings, Is.Empty);
        }
    }

    [Test]
    public void AChangeOfTheNotesChangesTheHash()
    {
        var hash = BflNotice.ComputeHash("## Welcome!\n\nThe notes.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflNotice.ComputeHash("## Welcome!\n\nThe changed notes."), Is.Not.EqualTo(hash));
            Assert.That(BflNotice.ComputeHash("## Herzlich willkommen!\n\nDie Hinweise."), Is.Not.EqualTo(hash), "Notes in another language were not read yet.");
        }
    }

    [Test]
    public void LineEndingsAndOuterSpaceDoNotChangeTheHash()
    {
        var hash = BflNotice.ComputeHash("First line\n\nSecond line");

        Assert.That(BflNotice.ComputeHash("  First line\r\n\r\nSecond line\r\n"), Is.EqualTo(hash));
    }

    [Test]
    public void OnlyTheHashOfTheNotesAsTheyAreNowCountsAsConfirmed()
    {
        var currentHash = BflNotice.ComputeHash(BflNotice.Text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(currentHash, Has.Length.EqualTo(64));
            Assert.That(BflNotice.IsAccepted(currentHash, currentHash), Is.True);
            Assert.That(BflNotice.IsAccepted(currentHash.ToUpperInvariant(), currentHash), Is.True);
            Assert.That(BflNotice.IsAccepted(string.Empty, currentHash), Is.False, "Nobody confirmed anything yet.");
            Assert.That(BflNotice.IsAccepted(BflNotice.ComputeHash("old"), currentHash), Is.False, "The notes changed since.");
        }
    }

    private static string NoticeSourceFile([CallerFilePath] string sourceFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "MindWork AI Studio", "Assistants", "BeyondFactsLab", "BflNotice.cs"));
}