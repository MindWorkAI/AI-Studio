using System.Runtime.CompilerServices;

using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which files the export menu finds in an answer.
/// </summary>
/// <remarks>
/// Asked for a web page, a model answers with a code block marked as html, and the same goes for a
/// LaTeX document or a Markdown text. That block already is the file the user wants. Converted along
/// with the rest of the answer, Pandoc shows it as a listing of source code instead, which is what
/// PR #993 reported. The fixture is the page attached to that PR, as the model wrote it.
/// </remarks>
[TestFixture]
public sealed class PlainFileExportTests
{
    private static readonly string PAGE = ReadFixture("standalone_page.html");

    [Test]
    public void AnAnswerMadeOfOneWebPageOffersThatPage()
    {
        var files = PlainFileExport.ExtractFiles(Lines("```html", PAGE, "```"), ',');

        Assert.Multiple(() =>
        {
            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(files[0].Format, Is.EqualTo(FileExportFormat.HTML));
            Assert.That(files[0].Content, Is.EqualTo(PAGE), "The page leaves the answer exactly as the model wrote it, without the fence around it.");
            Assert.That(files[0].Caption, Is.Empty, "Without a heading above it, a code block has nothing to be named after.");
        });
    }

    [Test]
    public void AWebPageAmidExplanationsIsOfferedAsWell()
    {
        var answer = Lines("Here is your page:", string.Empty, "```html", PAGE, "```", string.Empty, "Save it and open it in your browser.");

        var files = PlainFileExport.ExtractFiles(answer, ',');

        Assert.Multiple(() =>
        {
            Assert.That(files, Has.Count.EqualTo(1), "Models rarely answer with the block alone, so the text around it must not hide it.");
            Assert.That(files[0].Content, Is.EqualTo(PAGE));
        });
    }

    [TestCase("HTML", FileExportFormat.HTML)]
    [TestCase("tex", FileExportFormat.LATEX)]
    [TestCase("markdown", FileExportFormat.MARKDOWN)]
    [TestCase("html title=\"index.html\"", FileExportFormat.HTML, Description = "Whatever follows the language is an argument, not part of it.")]
    public void ACodeBlockIsOfferedInTheFormatItsLanguageNames(string infoString, FileExportFormat expectedFormat)
    {
        var files = PlainFileExport.ExtractFiles(Lines($"```{infoString}", "The content.", "```"), ',');

        Assert.Multiple(() =>
        {
            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(files[0].Format, Is.EqualTo(expectedFormat));
            Assert.That(files[0].Content, Is.EqualTo("The content."));
        });
    }

    [Test]
    public void ATildeFenceIsOfferedAsWell()
    {
        var files = PlainFileExport.ExtractFiles(Lines("~~~latex", @"\section{Results}", "~~~"), ',');

        Assert.That(files.Select(file => file.Format), Is.EqualTo(new[] { FileExportFormat.LATEX }));
    }

    [TestCase("```css", TestName = "A language AI Studio writes no file for")]
    [TestCase("```", TestName = "A fence without a language")]
    public void AnyOtherCodeBlockIsNotOffered(string openingFence)
    {
        var files = PlainFileExport.ExtractFiles(Lines(openingFence, "body { margin: 0; }", "```"), ',');

        Assert.That(files, Is.Empty);
    }

    [TestCase("```html", "<html><body><p>The answer broke off here", TestName = "Half a web page")]
    [TestCase("```csv", "Quarter,Revenue", TestName = "Half a table")]
    public void ACodeBlockTheModelNeverClosedIsNotOffered(string openingFence, string content)
    {
        var files = PlainFileExport.ExtractFiles(Lines("The answer starts normally.", string.Empty, openingFence, content), ',');

        Assert.That(files, Is.Empty, "The file would end wherever the answer broke off.");
    }

    [Test]
    public void TablesAndCodeBlocksAreCountedApart()
    {
        var answer = Lines(
            "# Revenue",
            string.Empty,
            "| Quarter | Revenue |",
            "|---|---|",
            "| Q1 | 100 |",
            string.Empty,
            "# Landing page",
            string.Empty,
            "```html",
            "<p>First block</p>",
            "```",
            string.Empty,
            "```latex",
            @"\section{Second block}",
            "```");

        var files = PlainFileExport.ExtractFiles(answer, ',');

        Assert.That(files.Select(file => (file.Ordinal, file.Caption, file.Format)), Is.EqualTo(new[]
        {
            (1, "Revenue", FileExportFormat.CSV),
            (1, "Landing page", FileExportFormat.HTML),
            (2, "Landing page", FileExportFormat.LATEX),
        }), "The first code block is code block 1, even though a table stands before it.");
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

    /// <summary>
    /// Reads a file from the fixtures next to this test.
    /// </summary>
    /// <remarks>
    /// Read from the source tree, the way the capability snapshot is, so the fixture needs no entry in
    /// the project file. A checkout on Windows may have turned its line ends into CRLF, which the
    /// model never wrote.
    /// </remarks>
    private static string ReadFixture(string fileName, [CallerFilePath] string sourceFilePath = "") => File
        .ReadAllText(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "Fixtures", fileName))
        .Replace("\r\n", "\n");
}