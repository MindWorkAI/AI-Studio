using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what AI Studio assumes about the readers of the formats it writes.
/// </summary>
[TestFixture]
public sealed class FileExportFormatTests
{
    [Test]
    public void OnlyTheTwoOfficeFormatsRefuseAPageInALocalLink()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileExportFormat.MICROSOFT_WORD.FollowsPageAnchors(), Is.False, "Word looks for a file whose name ends in the fragment, finds none, and refuses the link.");
            Assert.That(FileExportFormat.OPEN_DOCUMENT_TEXT.FollowsPageAnchors(), Is.False, "LibreOffice does the same, verified on 2026-09-15 with an exported .odt.");
            Assert.That(FileExportFormat.HTML.FollowsPageAnchors(), Is.True, "A browser opens the document on the page the fragment names.");
            Assert.That(FileExportFormat.MARKDOWN.FollowsPageAnchors(), Is.True);
            Assert.That(FileExportFormat.LATEX.FollowsPageAnchors(), Is.True);
        });
    }

    [Test]
    public void EveryFormatAnAnswerIsWrittenAsHasAnAnswerHere()
    {
        // Whoever adds a format decides what its reader can follow, rather than inheriting an
        // assumption. This fails for a format which nobody thought about, because the list below
        // has to name it:
        Assert.That(FileExportFormatExtensions.ANSWER_FORMATS, Is.EquivalentTo(new[]
        {
            FileExportFormat.MICROSOFT_WORD,
            FileExportFormat.OPEN_DOCUMENT_TEXT,
            FileExportFormat.LATEX,
            FileExportFormat.MARKDOWN,
            FileExportFormat.HTML,
        }), "A format was added to or removed from the export menu: say in FollowsPageAnchors whether its reader follows a page in a local link, then name it here.");
    }
}