using AIStudio.Components;
using AIStudio.Tools;

using Markdig.Syntax;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks the text which a code block AI Studio highlights itself shows and copies.
/// </summary>
[TestFixture]
public sealed class HighlightedMarkdownTests
{
    [TestCase("```python\nprint(1)\n\nx = 2\n```", "print(1)\n\nx = 2")]
    [TestCase("```python\r\nprint(1)\r\n\r\nx = 2\r\n```", "print(1)\r\n\r\nx = 2", Description = "Every line keeps its line break, the empty ones, too.")]
    [TestCase("```python\n    indented\n```", "    indented", Description = "The indentation is a part of the code.")]
    [TestCase("```python\n```", "", Description = "An empty block has no text.")]
    public void ACodeBlockShowsItsLines(string markdown, string expected)
    {
        var block = Markdig.Markdown.Parse(markdown, Markdown.CHAT_MARKDOWN_PIPELINE).Descendants<FencedCodeBlock>().Single();
        Assert.That(HighlightedMarkdown.TextOf(block), Is.EqualTo(expected));
    }
}