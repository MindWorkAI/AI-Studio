using System.Text;

using AIStudio.Tools.DocumentSegments;

namespace AIStudio.Tests.Tools.DocumentSegments;

/// <summary>
/// Checks which parts of a Markdown file a model works on, and that everything else stays as it is.
/// </summary>
[TestFixture]
public sealed class MarkdownSegmentFormatTests
{
    //
    // The line endings of this file depend on the checkout, so the document gets the ones of Markdown files on Linux and macOS:
    //
    private static readonly string DOCUMENT = """
                                    ---
                                    title: Hello
                                    ---

                                    # Getting started

                                    Read the [manual](https://example.org/manual "Manual") first.
                                    Then run `dotnet build` and **check** the output.

                                    - Install the *tools*
                                    - Open <https://example.org>

                                    > Quoted text
                                    > on two lines.

                                    | Name | Value |
                                    |------|-------|
                                    | Speed | 42 |

                                    ```bash
                                    echo "code stays"
                                    ```

                                    <div>HTML stays</div>

                                    Line one\
                                    line two
                                    """.ReplaceLineEndings("\n");

    private readonly MarkdownSegmentFormat format = new();
    private string testFolder = string.Empty;

    [SetUp]
    public void CreateFolder() => this.testFolder = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"))).FullName;

    [TearDown]
    public void RemoveFolder() => Directory.Delete(this.testFolder, true);

    [Test]
    public void TheWordsGoToTheModelAndTheMarkupBecomesTags()
    {
        var keys = MarkdownSegmentFormat.SegmentsOf(DOCUMENT).Select(segment => segment.Segment.Key).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(keys, Does.Contain("Getting started"));
            Assert.That(keys, Does.Contain("Read the <1>manual</1> first. Then run <2/> and <3>check</3> the output."), "Links and emphasis become pairs, code a tag of its own, and the soft line break a space.");
            Assert.That(keys, Does.Contain("Install the <1>tools</1>"));
            Assert.That(keys, Does.Contain("Open <1/>"), "An autolink is an address, not words.");
            Assert.That(keys, Does.Contain("Quoted text on two lines."), "The quote mark of the second line belongs to the line break.");
            Assert.That(keys, Is.SupersetOf(new[] { "Name", "Value", "Speed", "42" }));
            Assert.That(keys, Does.Contain("Line one<1/>line two"), "A hard line break is a tag of its own.");
            Assert.That(keys, Has.None.Contains("title"), "The front matter stays out.");
            Assert.That(keys, Has.None.Contains("code stays"), "Code blocks stay out.");
            Assert.That(keys, Has.None.Contains("HTML stays"), "HTML blocks stay out.");
        });
    }

    [Test]
    public async Task WritingTheSourceBackChangesNothing()
    {
        var source = await this.WriteSourceAsync(DOCUMENT);
        var target = Path.Join(this.testFolder, "target.md");

        var result = await this.format.WriteAsync(source, target, await this.ResultsAsync(source, text => text), string.Empty, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.WRITTEN));
            Assert.That(File.ReadAllText(target), Is.EqualTo(DOCUMENT));
        });
    }

    [Test]
    public async Task NewWordsKeepTheirMarkup()
    {
        var source = await this.WriteSourceAsync(DOCUMENT);
        var target = Path.Join(this.testFolder, "target.md");

        var result = await this.format.WriteAsync(source, target, await this.ResultsAsync(source, text => text.ToUpperInvariant()), "de-DE", CancellationToken.None);
        var written = File.ReadAllText(target);

        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.Zero);
            Assert.That(written, Does.StartWith("---\ntitle: Hello\n---"), "The front matter stays as it is.");
            Assert.That(written, Does.Contain("# GETTING STARTED"));
            Assert.That(written, Does.Contain("READ THE [MANUAL](https://example.org/manual \"Manual\") FIRST. THEN RUN `dotnet build` AND **CHECK** THE OUTPUT."));
            Assert.That(written, Does.Contain("- INSTALL THE *TOOLS*"));
            Assert.That(written, Does.Contain("- OPEN <https://example.org>"));
            Assert.That(written, Does.Contain("> QUOTED TEXT ON TWO LINES."));
            Assert.That(written, Does.Contain("| NAME | VALUE |"));
            Assert.That(written, Does.Contain("| SPEED | 42 |"));
            Assert.That(written, Does.Contain("echo \"code stays\""));
            Assert.That(written, Does.Contain("<div>HTML stays</div>"));
            Assert.That(written, Does.EndWith("LINE ONE\\\nLINE TWO"), "The hard line break stays with its backslash.");
        });
    }

    [Test]
    public async Task WordsWhoseTagsDoNotFitKeepTheMarkupWithoutWords()
    {
        const string DOCUMENT_WITH_CODE = "Run `dotnet build` and **check** the output.";
        var source = await this.WriteSourceAsync(DOCUMENT_WITH_CODE);
        var target = Path.Join(this.testFolder, "target.md");
        var results = new SegmentResults(new Dictionary<string, SegmentOutcome>
        {
            ["Run <1/> and <2>check</2> the output."] = SegmentOutcome.Transformed("Führe es aus und prüfe die Ausgabe."),
        });

        var result = await this.format.WriteAsync(source, target, results, "de-DE", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.EqualTo(1));
            Assert.That(File.ReadAllText(target), Is.EqualTo("Führe es aus und prüfe die Ausgabe. `dotnet build`"), "The code the model dropped follows the words rather than getting lost.");
        });
    }

    [Test]
    public async Task AChangedSourceWritesNothing()
    {
        var source = await this.WriteSourceAsync(DOCUMENT);
        var target = Path.Join(this.testFolder, "target.md");

        var result = await this.format.WriteAsync(source, target, new SegmentResults(new Dictionary<string, SegmentOutcome>()), "de-DE", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.SOURCE_CHANGED));
            Assert.That(File.Exists(target), Is.False);
        });
    }

    [Test]
    public async Task TheEncodingAndTheLineEndingsStay()
    {
        const string WINDOWS_DOCUMENT = "# Title\r\n\r\nSome text\r\nmore text.\r\n";
        var source = Path.Join(this.testFolder, "source.md");
        await File.WriteAllTextAsync(source, WINDOWS_DOCUMENT, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var target = Path.Join(this.testFolder, "target.md");

        await this.format.WriteAsync(source, target, await this.ResultsAsync(source, text => text.ToUpperInvariant()), "de-DE", CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(target);

        Assert.Multiple(() =>
        {
            Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "The byte order mark stays.");
            Assert.That(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Is.EqualTo("# TITLE\r\n\r\nSOME TEXT MORE TEXT.\r\n"));
        });
    }

    private async Task<string> WriteSourceAsync(string text)
    {
        var path = Path.Join(this.testFolder, "source.md");
        await File.WriteAllTextAsync(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private async Task<SegmentResults> ResultsAsync(string path, Func<string, string> transform)
    {
        var segments = await this.format.ReadAsync(path, CancellationToken.None);
        return new SegmentResults(segments
            .Where(segment => segment.HasWords)
            .DistinctBy(segment => segment.Key)
            .ToDictionary(segment => segment.Key, segment => SegmentOutcome.Transformed(transform(segment.Key))));
    }
}