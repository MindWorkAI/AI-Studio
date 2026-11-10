using System.Text;

using AIStudio.Tools.DocumentSegments;
using AIStudio.Tools.DocumentSegments.Html;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.DocumentSegments.Html;

/// <summary>
/// Checks which parts of a web page a model works on, and that everything else stays as it is.
/// </summary>
[TestFixture]
public sealed class HtmlSegmentFormatTests
{
    private const string PARAGRAPH = "Read the <1>manual</1> first.<2/> Then run <3/> & check the <4>output</4>.";

    //
    // The line endings of this file depend on the checkout, so the page gets the ones of web pages on Linux and macOS:
    //
    private static readonly string PAGE = """
                                          <!DOCTYPE html>
                                          <html lang="en">
                                          <head>
                                            <meta charset="utf-8">
                                            <title>Mission to Mars</title>
                                            <meta name="description" content="Why we fly to Mars">
                                            <style>p { color: red; }</style>
                                          </head>
                                          <body>
                                            <h1>Getting started</h1>
                                            <p>Read the <a href="https://example.org/manual" title="The manual">manual</a> first.<br>
                                               Then run <code>dotnet build</code> &amp; check the <strong>output</strong>.</p>
                                            <img src="mars.png" alt="The red planet">
                                            <ul>
                                              <li>Install the tools</li>
                                              <li>Open the app</li>
                                            </ul>
                                            <div>Text of its own<p>A paragraph</p></div>
                                            <pre>preformatted stays</pre>
                                            <script>var greeting = "script stays";</script>
                                          </body>
                                          </html>
                                          """.ReplaceLineEndings("\n");

    private readonly HtmlSegmentFormat format = new();
    private string testFolder = string.Empty;
    private string source = string.Empty;
    private string target = string.Empty;

    [SetUp]
    public void CreatePage()
    {
        this.testFolder = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"))).FullName;
        this.source = Path.Join(this.testFolder, "source.html");
        this.target = Path.Join(this.testFolder, "target.html");
        File.WriteAllText(this.source, PAGE, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(this.testFolder, true);

    [Test]
    public async Task TheWordsGoToTheModelAndTheMarkupBecomesTags()
    {
        var keys = (await this.format.ReadAsync(this.source, CancellationToken.None)).Select(segment => segment.Key).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(keys, Does.Contain("Mission to Mars"), "The title of the page is read.");
            Assert.That(keys, Does.Contain("Why we fly to Mars"), "The description of the page is read.");
            Assert.That(keys, Does.Contain("Getting started"));
            Assert.That(keys, Does.Contain(PARAGRAPH), "Links and emphasis are pairs, line breaks and code tags of their own, entities decoded, and white space collapsed.");
            Assert.That(keys, Does.Contain("The manual"), "The tooltip of the link is read.");
            Assert.That(keys, Does.Contain("The red planet"), "The alternative text of the picture is read.");
            Assert.That(keys, Is.SupersetOf(new[] { "Install the tools", "Open the app", "Text of its own", "A paragraph" }));
            Assert.That(keys, Has.None.Contains("stays"), "Preformatted text and scripts stay out.");
            Assert.That(keys, Has.None.Contains("color"), "Styles stay out.");
        });
    }

    [Test]
    public async Task WritingTheSourceBackChangesNothing()
    {
        var result = await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(key => key), string.Empty, NullLogger.Instance, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.WRITTEN));
            Assert.That(File.ReadAllText(this.target), Is.EqualTo(PAGE));
        });
    }

    [Test]
    public async Task NewWordsKeepTheirMarkup()
    {
        var result = await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(key => key.ToUpperInvariant()), "de-DE", NullLogger.Instance, CancellationToken.None);
        var written = File.ReadAllText(this.target);

        Assert.Multiple(() =>
        {
            Assert.That(result.SimplifiedCount, Is.Zero);
            Assert.That(written, Does.Contain("<html lang=\"de-DE\">"), "The page names its new language.");
            Assert.That(written, Does.Contain("<title>MISSION TO MARS</title>"));
            Assert.That(written, Does.Contain("<meta name=\"description\" content=\"WHY WE FLY TO MARS\">"));
            Assert.That(written, Does.Contain("<style>p { color: red; }</style>"));
            Assert.That(written, Does.Contain("<h1>GETTING STARTED</h1>"));
            Assert.That(written, Does.Contain("<p>READ THE <a href=\"https://example.org/manual\" title=\"THE MANUAL\">MANUAL</a> FIRST.<br> THEN RUN <code>dotnet build</code> &amp; CHECK THE <strong>OUTPUT</strong>.</p>"), "The address and the code stay, the tooltip is new, and the ampersand stays an entity.");
            Assert.That(written, Does.Contain("<img src=\"mars.png\" alt=\"THE RED PLANET\">"));
            Assert.That(written, Does.Contain("<li>INSTALL THE TOOLS</li>"));
            Assert.That(written, Does.Contain("<div>TEXT OF ITS OWN<p>A PARAGRAPH</p></div>"));
            Assert.That(written, Does.Contain("<pre>preformatted stays</pre>"));
            Assert.That(written, Does.Contain("<script>var greeting = \"script stays\";</script>"));
        });
    }

    [Test]
    public async Task NewWordsCannotBreakTheMarkup()
    {
        var results = await this.ResultsAsync(key => key switch
        {
            "Getting started" => "Erste <Schritte> & mehr",
            "The red planet" => "Der \"rote\" Planet",

            _ => key,
        });

        await this.format.WriteAsync(this.source, this.target, results, string.Empty, NullLogger.Instance, CancellationToken.None);
        var written = File.ReadAllText(this.target);

        Assert.Multiple(() =>
        {
            Assert.That(written, Does.Contain("<h1>Erste &lt;Schritte&gt; &amp; mehr</h1>"));
            Assert.That(written, Does.Contain("alt=\"Der &quot;rote&quot; Planet\""));
        });
    }

    [Test]
    public async Task APageWithoutLanguageGetsOne()
    {
        File.WriteAllText(this.source, "<html><body><p>Hello</p></body></html>", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await this.format.WriteAsync(this.source, this.target, await this.ResultsAsync(_ => "Hallo"), "de-DE", NullLogger.Instance, CancellationToken.None);

        Assert.That(File.ReadAllText(this.target), Is.EqualTo("<html lang=\"de-DE\"><body><p>Hallo</p></body></html>"));
    }

    [Test]
    public async Task AChangedSourceWritesNothing()
    {
        var result = await this.format.WriteAsync(this.source, this.target, new SegmentResults(new Dictionary<string, SegmentOutcome>()), "de-DE", NullLogger.Instance, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SegmentWriteOutcome.SOURCE_CHANGED));
            Assert.That(File.Exists(this.target), Is.False);
        });
    }

    private async Task<SegmentResults> ResultsAsync(Func<string, string> transform)
    {
        var segments = await this.format.ReadAsync(this.source, CancellationToken.None);
        return new SegmentResults(segments
            .Where(segment => segment.HasWords)
            .DistinctBy(segment => segment.Key)
            .ToDictionary(segment => segment.Key, segment => SegmentOutcome.Transformed(transform(segment.Key))));
    }
}