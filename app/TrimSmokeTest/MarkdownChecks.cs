using AIStudio.Tools;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// Lets HTMLParser convert what its tests convert, because ReverseMarkdown is marked as trimmable since version 6.1.
/// </summary>
/// <remarks>
/// The trimmer therefore removes every part of ReverseMarkdown which nothing but reflection reaches, and
/// Trimming.props suppresses the warnings which would name such a part. The converter of HTMLParser takes only the
/// built-in readers and writers, which need no reflection, and a preprocessing step of our own. The HTML and the
/// expected Markdown are those of HTMLParserConversionTests in app/Tests/Tools, which run untrimmed.
/// </remarks>
public static class MarkdownChecks
{
    private const string TEASER_HTML =
        """
        <ul>
        <li><a href="https://example.org/news/1"><div><header><h3>Probe lands on the asteroid</h3></header><footer><time>22 minutes ago</time><div><span>Example News</span></div></footer></div></a></li>
        <li><a href="https://example.org/news/2"><h3>Second headline</h3><p>A summary of it.</p></a></li>
        <li><a href="https://example.org/news/3">Plain link text</a></li>
        </ul>
        <p>Code in a link: <a href="https://example.org/code"><pre>line one
        line two</pre></a></p>
        """;

    private const string TEASER_MARKDOWN =
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

    private const string CONFIGURATION_HTML =
        """
        <h2>Heading &amp; more</h2>
        <p>Text with an <custom-tag>unknown tag</custom-tag>,<!-- a comment --> and a <a href="https://example.org">https://example.org</a> link.</p>
        <ol><li>First<ul><li>Nested</li></ul></li><li>Second</li></ol>
        <table><tr><th>Name</th><th>Value</th></tr><tr><td>A &lt; B</td><td>1</td></tr></table>
        <script>var hidden = 1;</script>
        """;

    private const string CONFIGURATION_MARKDOWN =
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

    /// <summary>
    /// Returns a description of every conversion which fails or gives other Markdown than untrimmed.
    /// </summary>
    /// <returns>One line per failure, naming the conversion and what went wrong.</returns>
    public static IReadOnlyList<string> FindFailures()
    {
        var failures = new List<string>();
        Expect(failures, "a teaser wrapped in a link", TEASER_HTML, TEASER_MARKDOWN);
        Expect(failures, "headings, lists, a table, and links", CONFIGURATION_HTML, CONFIGURATION_MARKDOWN);
        return failures;
    }

    /// <summary>
    /// Converts HTML and compares the Markdown with the expected one.
    /// </summary>
    /// <param name="failures">Where to add a failure.</param>
    /// <param name="what">What the HTML holds, completing "HTMLParser could not convert ...".</param>
    /// <param name="html">The HTML to convert.</param>
    /// <param name="expected">The Markdown the conversion gives untrimmed.</param>
    private static void Expect(List<string> failures, string what, string html, string expected)
    {
        string markdown;
        try
        {
            markdown = HTMLParser.ParseToMarkdown(html).ReplaceLineEndings("\n");
        }
        catch (Exception exception)
        {
            //
            // A member the trimmer removed surfaces as the innermost exception, e.g., below a TypeInitializationException:
            //
            var cause = exception.GetBaseException();
            failures.Add($"HTMLParser could not convert {what}: {cause.GetType().Name}: {cause.Message}");
            return;
        }

        if (markdown != expected.ReplaceLineEndings("\n"))
            failures.Add($"HTMLParser converted {what} into '{markdown}' instead of '{expected.ReplaceLineEndings("\n")}'.");
    }
}