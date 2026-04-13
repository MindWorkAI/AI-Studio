using System.Net;
using System.Net.Http;
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
        var response = await this.LoadWebPageAsync(url);
        return response.Document.ParsedText;
    }

    /// <summary>
    /// Loads the web content from the specified URL and returns it as an HTML string.
    /// </summary>
    /// <param name="url">The URL of the web page.</param>
    /// <returns>The web content as an HTML string.</returns>
    public async Task<string> LoadWebContentHTML(Uri url)
    {
        var response = await this.LoadWebPageAsync(url);
        var innerHtml = response.Document.DocumentNode.InnerHtml;

        return innerHtml;
    }

    public async Task<HTMLParserWebPage> LoadWebPageAsync(Uri url, CancellationToken token = default, int timeoutSeconds = 30)
    {
        using var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        using var response = await httpClient.GetAsync(url, timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(token);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        return new HTMLParserWebPage
        {
            RequestedUrl = url,
            FinalUrl = response.RequestMessage?.RequestUri ?? url,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            Document = document,
        };
    }

    public string ExtractTitle(HtmlDocument document)
    {
        var title = document.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
        return WebUtility.HtmlDecode(title ?? string.Empty).Trim();
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

public sealed class HTMLParserWebPage
{
    public required Uri RequestedUrl { get; init; }

    public required Uri FinalUrl { get; init; }

    public required string ContentType { get; init; }

    public required HtmlDocument Document { get; init; }
}
