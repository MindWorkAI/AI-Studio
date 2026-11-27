using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the Markdown which the HTML of a web page or a mail becomes before a model reads it.
/// </summary>
/// <remarks>
/// The trim smoke test converts the same HTML in a trimmed build and expects the same Markdown, see MarkdownChecks
/// in app/TrimSmokeTest. A change here belongs there as well.
/// </remarks>
[TestFixture]
public sealed class HTMLParserConversionTests
{
    /// <summary>
    /// A teaser wrapped in a link keeps the words of its blocks apart, while the text of pre stays as it is.
    /// </summary>
    /// <remarks>
    /// Without the compatibility shim in HTMLParser, the first item reads "Probe lands on the asteroid22 minutes
    /// agoExample News", see documentation/compatibility-shims/2026-11-reversemarkdown-blocks-in-inline-elements.md.
    /// </remarks>
    [Test]
    public void BlocksInsideALinkKeepTheirWordsApart()
    {
        const string HTML =
            """
            <ul>
            <li><a href="https://example.org/news/1"><div><header><h3>Probe lands on the asteroid</h3></header><footer><time>22 minutes ago</time><div><span>Example News</span></div></footer></div></a></li>
            <li><a href="https://example.org/news/2"><h3>Second headline</h3><p>A summary of it.</p></a></li>
            <li><a href="https://example.org/news/3">Plain link text</a></li>
            </ul>
            <p>Code in a link: <a href="https://example.org/code"><pre>line one
            line two</pre></a></p>
            """;

        const string EXPECTED =
            """
            - [Probe lands on the asteroid 22 minutes ago Example News](https://example.org/news/1)
            - [Second headline A summary of it.](https://example.org/news/2)
            - [Plain link text](https://example.org/news/3)

            Code in a link: [](https://example.org/code)

            ```
            line one
            line two
            ```
            """;

        Assert.That(HTMLParser.ParseToMarkdown(HTML).ReplaceLineEndings("\n"), Is.EqualTo(EXPECTED.ReplaceLineEndings("\n")));
    }

    /// <summary>
    /// The conversion keeps the text of unknown tags, drops comments and scripts, writes a link whose text is its
    /// address as that address, and decodes the HTML entities.
    /// </summary>
    [Test]
    public void TheConversionFollowsItsConfiguration()
    {
        const string HTML =
            """
            <h2>Heading &amp; more</h2>
            <p>Text with an <custom-tag>unknown tag</custom-tag>,<!-- a comment --> and a <a href="https://example.org">https://example.org</a> link.</p>
            <ol><li>First<ul><li>Nested</li></ul></li><li>Second</li></ol>
            <table><tr><th>Name</th><th>Value</th></tr><tr><td>A &lt; B</td><td>1</td></tr></table>
            <script>var hidden = 1;</script>
            """;

        const string EXPECTED =
            """
            ## Heading & more

            Text with an unknown tag, and a https://example.org link.

            1. First
               - Nested
            2. Second

            | Name | Value |
            | --- | --- |
            | A &lt; B | 1 |
            """;

        Assert.That(HTMLParser.ParseToMarkdown(HTML).ReplaceLineEndings("\n"), Is.EqualTo(EXPECTED.ReplaceLineEndings("\n")));
    }
}