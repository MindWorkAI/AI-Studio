using System.Runtime.CompilerServices;

using AIStudio.Tools;

using Markdig.Syntax;

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(files[0].Format, Is.EqualTo(FileExportFormat.HTML));
            Assert.That(files[0].Content, Is.EqualTo(PAGE), "The page leaves the answer exactly as the model wrote it, without the fence around it.");
            Assert.That(files[0].Caption, Is.Empty, "Without a heading above it, a code block has nothing to be named after.");
        }
    }

    [Test]
    public void AWebPageAmidExplanationsIsOfferedAsWell()
    {
        var answer = Lines("Here is your page:", string.Empty, "```html", PAGE, "```", string.Empty, "Save it and open it in your browser.");

        var files = PlainFileExport.ExtractFiles(answer, ',');

        using (Assert.EnterMultipleScope())
        {
            Assert.That(files, Has.Count.EqualTo(1), "Models rarely answer with the block alone, so the text around it must not hide it.");
            Assert.That(files[0].Content, Is.EqualTo(PAGE));
        }
    }

    [TestCase("HTML", FileExportFormat.HTML)]
    [TestCase("tex", FileExportFormat.LATEX)]
    [TestCase("markdown", FileExportFormat.MARKDOWN)]
    [TestCase("html title=\"index.html\"", FileExportFormat.HTML, Description = "Whatever follows the language is an argument, not part of it.")]
    public void ACodeBlockIsOfferedInTheFormatItsLanguageNames(string infoString, FileExportFormat expectedFormat)
    {
        var files = PlainFileExport.ExtractFiles(Lines($"```{infoString}", "The content.", "```"), ',');

        using (Assert.EnterMultipleScope())
        {
            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(files[0].Format, Is.EqualTo(expectedFormat));
            Assert.That(files[0].Content, Is.EqualTo("The content."));
        }
    }

    [Test]
    public void ATildeFenceIsOfferedAsWell()
    {
        var files = PlainFileExport.ExtractFiles(Lines("~~~latex", @"\section{Results}", "~~~"), ',');

        Assert.That(files.Select(file => file.Format), Is.EqualTo(new[] { FileExportFormat.LATEX }));
    }

    [TestCase("```python", "Python (.py)")]
    [TestCase("```rust", "Rust (.rs)")]
    [TestCase("```css", "CSS (.css)")]
    [TestCase("~~~bash", "Shell (.sh)")]
    [TestCase("```foo", "foo (.txt)", Description = "A language AI Studio does not know still names the block.")]
    [TestCase("```", "Text (.txt)", Description = "A block without a language is plain text.")]
    public void ACodeBlockInAnyOtherLanguageIsOfferedAsCode(string openingFence, string expectedName)
    {
        var files = PlainFileExport.ExtractFiles(Lines(openingFence, "print(42)", openingFence[..3]), ',');

        using (Assert.EnterMultipleScope())
        {
            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(files[0].Format, Is.EqualTo(FileExportFormat.SOURCE_CODE));
            Assert.That(files[0].ToName(), Is.EqualTo(expectedName));
            Assert.That(files[0].Content, Is.EqualTo("print(42)"), "The code leaves the answer exactly as the model wrote it, without the fence around it.");
        }
    }

    [Test]
    public void AFormulaIsNoCodeBlock()
    {
        var answer = Lines("The energy follows from", string.Empty, "$$", "E = mc^2", "$$", string.Empty, "for every body at rest.");

        var files = PlainFileExport.ExtractFiles(answer, ',');

        Assert.That(files, Is.Empty, "Markdig reads a formula as a fenced block, but nobody saves a formula as a text file.");
    }

    [TestCase("```html", "<html><body><p>The answer broke off here", TestName = "Half a web page")]
    [TestCase("```csv", "Quarter,Revenue", TestName = "Half a table")]
    [TestCase("```python", "def main():", TestName = "Half a program")]
    [TestCase("```", "The answer broke off here", TestName = "Half a text")]
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
            "```",
            string.Empty,
            "# Script",
            string.Empty,
            "```python",
            "print('Third block')",
            "```");

        var files = PlainFileExport.ExtractFiles(answer, ',');

        Assert.That(files.Select(file => (file.Ordinal, file.Caption, file.Format)), Is.EqualTo(new[]
        {
            (1, "Revenue", FileExportFormat.CSV),
            (1, "Landing page", FileExportFormat.HTML),
            (2, "Landing page", FileExportFormat.LATEX),
            (3, "Script", FileExportFormat.SOURCE_CODE),
        }), "The first code block is code block 1, even though a table stands before it, and code counts along with the other blocks.");
    }

    [Test]
    public void EachFileKnowsWhereItStartsInTheAnswer()
    {
        var answer = Lines(
            "Here is the revenue:",
            string.Empty,
            "| Quarter | Revenue |",
            "|---|---|",
            "| Q1 | 100 |",
            string.Empty,
            "And the script which made it:",
            string.Empty,
            "```python",
            "print(100)",
            "```");

        var files = PlainFileExport.ExtractFiles(answer, ',');

        Assert.That(files.Select(file => file.Position), Is.EqualTo(new[] { answer.IndexOf("| Quarter", StringComparison.Ordinal), answer.IndexOf("```python", StringComparison.Ordinal) }));
    }

    [Test]
    public void ACodeBlockInAListKnowsWhereItStarts()
    {
        var answer = Lines(
            "1. Install the package:",
            string.Empty,
            "   ```bash",
            "   pip install requests",
            "   ```",
            "2. Run your script.");

        var files = PlainFileExport.ExtractFiles(answer, ',');

        Assert.That(files.Select(file => file.Position), Is.EqualTo(new[] { answer.IndexOf("```bash", StringComparison.Ordinal) }));
    }

    /// <summary>
    /// Checks what a code block the chat renders relies on to find its file.
    /// </summary>
    /// <remarks>
    /// The chat renders an answer in parts, cut around each formula, and Markdig reads every part on
    /// its own. A block of such a part therefore knows where it starts within its part only. Added to
    /// where the part starts in the answer, that has to be the position the file was read with.
    /// </remarks>
    [Test]
    public void ACodeBlockAfterAFormulaFindsItsFile()
    {
        var answer = Lines(
            "# Script",
            string.Empty,
            "```python",
            "print(1)",
            "```",
            string.Empty,
            "$$",
            "E = mc^2",
            "$$",
            string.Empty,
            "```rust",
            "fn main() {}",
            "```");

        var files = PlainFileExport.ExtractFiles(answer, ',');
        var partStart = answer.LastIndexOf("$$", StringComparison.Ordinal) + "$$".Length + Environment.NewLine.Length;
        var part = Markdig.Markdown.Parse(answer[partStart..], Markdown.CHAT_MARKDOWN_PIPELINE);
        var blockOfThePart = part.Descendants<FencedCodeBlock>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(files, Has.Count.EqualTo(2));
            Assert.That(partStart + blockOfThePart.Span.Start, Is.EqualTo(files[1].Position));
            Assert.That(files[1].ToName(), Is.EqualTo("Rust (.rs)"));
        }
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