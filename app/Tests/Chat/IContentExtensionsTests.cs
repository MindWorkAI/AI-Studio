using AIStudio.Chat;
using AIStudio.Tools;

using Markdig.Syntax;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that an answer leaves AI Studio together with the sources it rests on.
/// </summary>
/// <remarks>
/// The sources under an answer come from AI Studio, not from the model, so they are not part of the
/// text a file writer reads. With RAG and web search in v26.9.1 that is most of what makes an answer
/// checkable: a document which says a page was read, without saying which one, is worth little to
/// whoever receives it. The chat renders the answer and the sources as two texts, which hides every
/// way the one can run into the other -- an open code fence above all. A document has no such seam.
/// </remarks>
[TestFixture]
public sealed class IContentExtensionsTests
{
    private static readonly Source TOOL_SOURCE = new("Search result", "https://example.org/search", SourceOrigin.TOOL);

    [Test]
    public void TheSourcesFollowTheAnswer()
    {
        var content = TextWith("The answer of the model ends here.", TOOL_SOURCE);

        var found = content.TryGetExportMarkdown(out var markdown);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(markdown, Does.EndWith(content.Sources.ToExportMarkdown()), "What the chat shows below the answer is what the file holds below it.");
            Assert.That(TopLevelBlocksOf(markdown), Is.EqualTo(new[] { "ParagraphBlock", "h1", "h2", "ListBlock" }), "The answer stays a paragraph of its own; the source list starts under its own heading.");
        });
    }

    [Test]
    public void AnAnswerEndingInATableKeepsIt()
    {
        var content = TextWith(Lines(
            "Here are the numbers:",
            string.Empty,
            "| Quarter | Revenue |",
            "|---|---|",
            "| Q1 | 100 |"), TOOL_SOURCE);

        var found = content.TryGetExportMarkdown(out var markdown);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(TopLevelBlocksOf(markdown), Is.EqualTo(new[] { "ParagraphBlock", "Table", "h1", "h2", "ListBlock" }), "The table ends where it ended; the headings below it are not two more rows.");
        });
    }

    [Test]
    public void AnAnswerEndingInAListKeepsIt()
    {
        var content = TextWith(Lines(
            "Three points:",
            string.Empty,
            "- one",
            "- two",
            "- three"), TOOL_SOURCE);

        var found = content.TryGetExportMarkdown(out var markdown);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(TopLevelBlocksOf(markdown), Is.EqualTo(new[] { "ParagraphBlock", "ListBlock", "h1", "h2", "ListBlock" }), "Two lists, not one: the sources do not become the fourth point of the answer.");
        });
    }

    [Test]
    public void AnOpenCodeFenceDoesNotSwallowTheSources()
    {
        // Either the model forgot the closing fence, or the answer was cut short. Both happen, and
        // in a document both would turn everything below into code:
        var content = TextWith(Lines(
            "Here is the code:",
            string.Empty,
            "```csharp",
            "var answer = 42;"), TOOL_SOURCE);

        var found = content.TryGetExportMarkdown(out var markdown);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(TopLevelBlocksOf(markdown), Is.EqualTo(new[] { "ParagraphBlock", "FencedCodeBlock", "h1", "h2", "ListBlock" }), "The code block is closed for the model, so the sources stand below it instead of inside it.");
        });
    }

    [Test]
    public void WithoutSourcesNothingIsAdded()
    {
        const string ANSWER = "  An answer nobody had to look anything up for.  ";
        var content = TextWith(ANSWER);

        var found = content.TryGetExportMarkdown(out var markdown);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(markdown, Is.EqualTo(ANSWER.Trim()), "The everyday case: no heading, no empty line, nothing anybody has to explain.");
        });
    }

    [Test]
    public void WithoutAnAnswerTheSourcesStandAlone()
    {
        var content = TextWith(string.Empty, TOOL_SOURCE);

        var found = content.TryGetExportMarkdown(out var markdown);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(markdown, Is.EqualTo(content.Sources.ToExportMarkdown()), "Nothing above the heading means no empty line above it either.");
        });
    }

    [Test]
    public void APictureHasNothingToExport()
    {
        IContent picture = new ContentImage
        {
            SourceType = ContentImageSource.URL,
            Source = "https://example.org/picture.png",
            Sources = [TOOL_SOURCE],
        };

        Assert.Multiple(() =>
        {
            Assert.That(picture.TryGetExportMarkdown(out var markdown), Is.False, "There is no text document for a picture, so the caller hears no and says so.");
            Assert.That(markdown, Is.Empty, "A file writer must not put an excuse into the file it writes.");
        });
    }

    [Test]
    public void TheTableReadingStaysTheTextOfTheModel()
    {
        const string ANSWER = "  An answer with a source hanging on it.  ";
        var content = TextWith(ANSWER, TOOL_SOURCE);

        var found = content.TryGetMarkdownText(out var markdown);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(markdown, Is.EqualTo(ANSWER), "Neither trimmed nor extended: whoever reads a table out of a message wants what the model wrote and nothing else.");
        });
    }

    [Test]
    public void ATableExportCarriesNoSources()
    {
        var content = TextWith(Lines(
            "| Quarter | Revenue |",
            "|---|---|",
            "| Q1 | 100 |"), TOOL_SOURCE);

        content.TryGetMarkdownText(out var markdown);
        var tables = PlainFileExport.ExtractFiles(markdown, ',');

        Assert.Multiple(() =>
        {
            Assert.That(tables, Has.Count.EqualTo(1), "One table in the message, one table offered for it.");
            Assert.That(tables[0].Content, Does.Not.Contain("example.org"), "A data table has no column a link list would fit into.");
        });
    }

    /// <summary>
    /// A text message with the given sources hanging on it.
    /// </summary>
    /// <param name="text">The text the model wrote.</param>
    /// <param name="sources">The sources AI Studio collected for it.</param>
    /// <returns>The content.</returns>
    private static ContentText TextWith(string text, params Source[] sources) => new()
    {
        Text = text,
        Sources = [..sources],
    };

    /// <summary>
    /// Names the blocks a Markdown text is made of, headings by their level.
    /// </summary>
    /// <remarks>
    /// Only the blocks of the document itself, not the ones nested in them: whether the source list
    /// stands on the document or inside the last block of the answer is the whole question here.
    /// Markdig hangs a group for link reference definitions at the end of every document, which
    /// carries no text and is left out.
    /// </remarks>
    /// <param name="markdown">The Markdown text to read.</param>
    /// <returns>The names, in the order the blocks stand in.</returns>
    private static IReadOnlyList<string> TopLevelBlocksOf(string markdown) => Markdig.Markdown
        .Parse(markdown, Markdown.SAFE_MARKDOWN_PIPELINE)
        .Where(block => block is not LinkReferenceDefinitionGroup)
        .Select(block => block is HeadingBlock heading ? $"h{heading.Level}" : block.GetType().Name)
        .ToList();

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);
}