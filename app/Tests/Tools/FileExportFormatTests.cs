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

    [TestCase("html", FileExportFormat.HTML)]
    [TestCase("latex", FileExportFormat.LATEX)]
    [TestCase("tex", FileExportFormat.LATEX)]
    [TestCase("markdown", FileExportFormat.MARKDOWN)]
    [TestCase("md", FileExportFormat.MARKDOWN)]
    [TestCase("csv", FileExportFormat.CSV)]
    [TestCase("tsv", FileExportFormat.TSV)]
    [TestCase("HTML", FileExportFormat.HTML, Description = "Models do not agree on the case.")]
    [TestCase("LaTeX", FileExportFormat.LATEX)]
    [TestCase("CSV", FileExportFormat.CSV)]
    [TestCase(" md ", FileExportFormat.MARKDOWN, Description = "Space around the name is no part of it.")]
    public void AFenceLanguageNamesItsFormat(string language, FileExportFormat expectedFormat)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileExportFormatExtensions.TryFromCodeFenceLanguage(language, out var format), Is.True);
            Assert.That(format, Is.EqualTo(expectedFormat));
            Assert.That(format.IsPlainText(), Is.True, "A code block holds text, so the export writes it as it is.");
        });
    }

    [Test]
    public void OnlyTheTwoOfficeFormatsAreNoPlainText()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileExportFormat.MICROSOFT_WORD.IsPlainText(), Is.False, "A Word file is an archive, and writing text into one breaks it.");
            Assert.That(FileExportFormat.OPEN_DOCUMENT_TEXT.IsPlainText(), Is.False);
            Assert.That(FileExportFormat.NONE.IsPlainText(), Is.False, "No format means no file.");
            Assert.That(FileExportFormat.UNKNOWN.IsPlainText(), Is.False);
            Assert.That(FileExportFormat.HTML.IsPlainText(), Is.True, "A page the model wrote is a finished file, even though an entire answer needs Pandoc to become one.");
            Assert.That(FileExportFormat.LATEX.IsPlainText(), Is.True);
            Assert.That(FileExportFormat.MARKDOWN.IsPlainText(), Is.True);
            Assert.That(FileExportFormat.CSV.IsPlainText(), Is.True);
            Assert.That(FileExportFormat.TSV.IsPlainText(), Is.True);
        });
    }

    [TestCase("css", TestName = "A language AI Studio writes no file for")]
    [TestCase("docx", TestName = "A format no code block can hold")]
    [TestCase("", TestName = "A fence without a language")]
    [TestCase(null, TestName = "A fence Markdig read no language for")]
    public void AnyOtherFenceLanguageNamesNoFormat(string? language)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileExportFormatExtensions.TryFromCodeFenceLanguage(language, out var format), Is.False);
            Assert.That(format, Is.EqualTo(FileExportFormat.NONE));
        });
    }

    [TestCase(FileExportFormat.HTML)]
    [TestCase(FileExportFormat.MARKDOWN)]
    public void AnHtmlCommentEndsWhereItShouldAndNowhereElse(FileExportFormat format)
    {
        var found = format.TryToComment("A page titled --> Start, and one titled --!> Next", out var comment);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(comment, Does.StartWith("<!--"));
            Assert.That(comment.IndexOf("-->", StringComparison.Ordinal), Is.EqualTo(comment.Length - 3), "Only the end of the comment may end it; the title would spill onto the page otherwise.");
            Assert.That(comment, Does.Not.Contain("--!>"), "A browser ends a comment there as well.");
            Assert.That(comment, Does.Contain("Start").And.Contain("Next"), "The title stays readable.");
        });
    }

    [Test]
    public void AnHtmlCommentKeepsTheDashesOfAnAddress()
    {
        FileExportFormat.HTML.TryToComment("https://xn--mnchen-3ya.de/", out var comment);

        Assert.That(comment, Does.Contain("https://xn--mnchen-3ya.de/"), "A domain with an umlaut is written with two dashes, and the link has to keep working.");
    }

    [Test]
    public void EveryLineOfALatexCommentIsOne()
    {
        var found = FileExportFormat.LATEX.TryToComment(Lines("# Sources", string.Empty, "- [1] A title with 100 % and a_b"), out var comment);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(comment.Split(Environment.NewLine), Is.EqualTo(new[] { "% # Sources", "%", "% - [1] A title with 100 % and a_b" }), "LaTeX has no end of a comment, only the end of a line.");
        });
    }

    [TestCase(FileExportFormat.CSV)]
    [TestCase(FileExportFormat.TSV)]
    [TestCase(FileExportFormat.MICROSOFT_WORD)]
    public void AFormatWithoutCommentsSaysSo(FileExportFormat format)
    {
        Assert.Multiple(() =>
        {
            Assert.That(format.TryToComment("A text.", out var comment), Is.False);
            Assert.That(comment, Is.Empty);
        });
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);
}