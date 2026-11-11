using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Tools;

using Markdig.Extensions.Mathematics;
using Markdig.Syntax;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks which code blocks of an answer get actions, such as a button to save them.
/// </summary>
/// <remarks>
/// A block finds its file by where it starts. The chat renders the answer with a pipeline of its
/// own, so the blocks are read the same way here; how an answer cut around a formula keeps its
/// positions is checked in PlainFileExportTests.
/// </remarks>
[TestFixture]
public sealed class ExportableMarkdownTests
{
    [Test]
    public void EachCodeBlockFindsItsFile()
    {
        var answer = Lines(
            "| Quarter | Revenue |",
            "|---|---|",
            "| Q1 | 100 |",
            string.Empty,
            "```python",
            "print(100)",
            "```",
            string.Empty,
            "```html",
            "<p>100</p>",
            "```");

        var files = PlainFileExport.ExtractFiles(MarkdownRenderPlanner.Build(answer), ',');
        var foundFiles = Render(answer).Descendants<FencedCodeBlock>().Select(block => ExportableMarkdown.FileAt(files, block.Span.Start));

        Assert.That(foundFiles, Is.EqualTo(new[] { files[1], files[2] }), "The table comes first among the files, but it is no code block.");
    }

    [Test]
    public void AFormulaIsNoPartOfTheMarkdown()
    {
        var answer = Lines("1. The energy follows from", string.Empty, "   $$", "   E = mc^2", "   $$");

        var plan = MarkdownRenderPlanner.Build(answer);
        var formulasOfTheMarkdown = plan.Segments
            .Where(segment => segment.Type is MarkdownRenderSegmentType.MARKDOWN)
            .SelectMany(segment => Render(segment.GetContent(plan.Source)).Descendants<MathBlock>());

        Assert.That(formulasOfTheMarkdown, Is.Empty, "The chat renders a formula and its actions itself, so the Markdown around it holds no formula to give a code block's actions to.");
    }

    [Test]
    public void EveryFormulaFindsItsFile()
    {
        var answer = Lines(
            "$$",
            "a = b",
            "$$",
            @"\[",
            "c = d",
            @"\]",
            "```latex",
            "e = f",
            "```",
            "1. Item",
            "   $$",
            "   g = h",
            "   $$");

        var plan = MarkdownRenderPlanner.Build(answer);
        var files = PlainFileExport.ExtractFiles(plan, ',');
        var foundFiles = plan.Segments
            .Where(segment => segment.Type is MarkdownRenderSegmentType.MATH_BLOCK)
            .Select(segment => ExportableMarkdown.FileAt(files, segment.FileStart)?.Format);

        Assert.That(foundFiles, Is.EqualTo(Enumerable.Repeat<FileExportFormat?>(FileExportFormat.LATEX, 4)), "The chat looks a formula's file up where its block starts, so that the formula gets the button to save it.");
    }

    [Test]
    public void ACodeBlockTheModelNeverClosedFindsNoFile()
    {
        var answer = Lines("The answer starts normally.", string.Empty, "```python", "def main():");

        var block = Render(answer).Descendants<FencedCodeBlock>().Single();

        Assert.That(ExportableMarkdown.FileAt(PlainFileExport.ExtractFiles(MarkdownRenderPlanner.Build(answer), ','), block.Span.Start), Is.Null, "The file would end wherever the answer broke off.");
    }

    private static MarkdownDocument Render(string answer) => Markdig.Markdown.Parse(answer, Markdown.CHAT_MARKDOWN_PIPELINE);

    private static string Lines(params string[] lines) => string.Join('\n', lines);
}