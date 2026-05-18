using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using HtmlAgilityPack;

using ReverseMarkdown;

namespace AIStudio.Tools;

public sealed class HTMLParser
{
    private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) MindWorkAIStudio/1.0";
    private const int MAX_REDIRECTS = 10;

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

    public async Task<HTMLParserWebPage> LoadWebPageAsync(Uri url, CancellationToken token = default, int timeoutSeconds = 30, Func<Uri, CancellationToken, Task>? validateUrlAsync = null)
    {
        using var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect = false,
        };
        using var httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var currentUrl = url;
        for (var redirectCount = 0; redirectCount <= MAX_REDIRECTS; redirectCount++)
        {
            if (validateUrlAsync is not null)
                await validateUrlAsync(currentUrl, timeoutCts.Token);

            using var request = CreateRequest(currentUrl);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (IsRedirect(response.StatusCode))
            {
                if (response.Headers.Location is null)
                    throw new HttpRequestException($"The server returned a redirect without a Location header for '{currentUrl}'.", null, response.StatusCode);

                currentUrl = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUrl, response.Headers.Location);

                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var reasonPhrase = string.IsNullOrWhiteSpace(response.ReasonPhrase) ? "Unknown" : response.ReasonPhrase;
                throw new HttpRequestException($"The server returned HTTP {statusCode} ({reasonPhrase}) for '{currentUrl}'.", null, response.StatusCode);
            }

            var html = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            var document = new HtmlDocument();
            document.LoadHtml(html);

            return new HTMLParserWebPage
            {
                RequestedUrl = url,
                FinalUrl = response.RequestMessage?.RequestUri ?? currentUrl,
                ContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty,
                Document = document,
            };
        }

        throw new HttpRequestException($"The server returned more than {MAX_REDIRECTS} redirects for '{url}'.");
    }

    private static HttpRequestMessage CreateRequest(Uri url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", USER_AGENT);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.9));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
        return request;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => (int)statusCode is >= 300 and <= 399;

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
