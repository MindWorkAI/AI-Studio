using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using HtmlAgilityPack;
using ReverseMarkdown;

namespace AIStudio.Tools;

public sealed class HTMLParser
{
    private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) MindWorkAIStudio/1.0";
    private const int MAX_REDIRECTS = 10;
    private const int DEFAULT_MAX_RESPONSE_BYTES = 5 * 1024 * 1024;

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

    public async Task<HTMLParserWebPage> LoadWebPageAsync(Uri url, CancellationToken token = default, int timeoutSeconds = 30, Func<Uri, CancellationToken, Task<IReadOnlyList<IPAddress>>>? resolveUrlAddressesAsync = null, int maxResponseBytes = DEFAULT_MAX_RESPONSE_BYTES)
    {
        using var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect = false,
        };
        if (resolveUrlAddressesAsync is not null)
        {
            // The callback binds the request to a vetted target IP; a proxy would change the endpoint being connected to.
            handler.UseProxy = false;
            handler.ConnectCallback = async (context, connectionToken) => await ConnectToResolvedAddressAsync(context, resolveUrlAddressesAsync, connectionToken);
        }

        using var httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var currentUrl = url;
        for (var redirectCount = 0; redirectCount <= MAX_REDIRECTS; redirectCount++)
        {
            ValidateHttpOrHttpsUrl(currentUrl);

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

            var html = await ReadContentAsStringWithLimitAsync(response.Content, maxResponseBytes, timeoutCts.Token);
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

    private static void ValidateHttpOrHttpsUrl(Uri url)
    {
        if (url.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return;

        throw new HttpRequestException($"Unsupported URL scheme '{url.Scheme}' for '{url}'.");
    }

    private static async ValueTask<Stream> ConnectToResolvedAddressAsync(
        SocketsHttpConnectionContext context,
        Func<Uri, CancellationToken, Task<IReadOnlyList<IPAddress>>> resolveUrlAddressesAsync,
        CancellationToken token)
    {
        var requestUri = context.InitialRequestMessage.RequestUri ??
                         throw new HttpRequestException("The HTTP request did not contain a target URL.");

        var addresses = await resolveUrlAddressesAsync(requestUri, token);
        if (addresses.Count == 0)
            throw new HttpRequestException($"The host '{requestUri.Host}' did not resolve to an IP address.");

        List<SocketException> connectionErrors = [];
        foreach (var address in addresses.Distinct())
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                connectionErrors.Add(exception);
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        Exception innerException = connectionErrors.Count == 1
            ? connectionErrors[0]
            : new AggregateException(connectionErrors);
        throw new HttpRequestException($"Could not connect to a validated address for '{requestUri.Host}'.", innerException);
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

    private static async Task<string> ReadContentAsStringWithLimitAsync(HttpContent content, int maxResponseBytes, CancellationToken token)
    {
        if (content.Headers.ContentLength is long contentLength && contentLength > maxResponseBytes)
            throw new HttpRequestException($"The response body is too large. Maximum allowed size is {maxResponseBytes} bytes.");

        await using var stream = await content.ReadAsStreamAsync(token);
        await using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, token);
            if (read == 0)
                break;

            if (buffer.Length + read > maxResponseBytes)
                throw new HttpRequestException($"The response body is too large. Maximum allowed size is {maxResponseBytes} bytes.");

            buffer.Write(chunk, 0, read);
        }

        var encoding = TryGetContentEncoding(content) ?? Encoding.UTF8;
        return encoding.GetString(buffer.ToArray());
    }

    private static Encoding? TryGetContentEncoding(HttpContent content)
    {
        var charset = content.Headers.ContentType?.CharSet?.Trim();
        if (string.IsNullOrWhiteSpace(charset))
            return null;

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch
        {
            return null;
        }
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
