using System.Net;
using System.Text;

using HtmlAgilityPack;

using ReverseMarkdown;

namespace AIStudio.Tools;

public sealed class HTMLParser
{
    private static readonly Config MARKDOWN_PARSER_CONFIG = new()
    {
        UnknownTags = Config.UnknownTagsOption.Bypass,
        RemoveComments = true,
        SmartHrefHandling = true
    };

    /// <summary>
    /// Loads the web content from the specified URL.
    /// </summary>
    /// <param name="url">The URL of the web page.</param>
    /// <returns>The web content as text.</returns>
    public async Task<string> LoadWebContentText(Uri url)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var parser = new HtmlWeb();
        var doc = await parser.LoadFromWebAsync(url, Encoding.UTF8, new NetworkCredential(), cts.Token);
        return doc.ParsedText;
    }

    /// <summary>
    /// Loads the web content from the specified URL and returns it as an HTML string.
    /// </summary>
    /// <param name="url">The URL of the web page.</param>
    /// <returns>The web content as an HTML string.</returns>
    public async Task<string> LoadWebContentHTML(Uri url)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var parser = new HtmlWeb();
        var doc = await parser.LoadFromWebAsync(url, Encoding.UTF8, new NetworkCredential(), cts.Token);
        var innerHtml = doc.DocumentNode.InnerHtml;

        return innerHtml;
    }

    /// <summary>
    /// Converts HTML content to the Markdown format.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>The converted Markdown content.</returns>
    public string ParseToMarkdown(string html)
    {
        var markdownConverter = new Converter(MARKDOWN_PARSER_CONFIG);
        return markdownConverter.Convert(html);
    }
}