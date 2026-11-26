using System.Collections.Frozen;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using AIStudio.Tools.Web;
using AngleSharp.Dom;
using HtmlAgilityPack;
using ReverseMarkdown;

namespace AIStudio.Tools;

public sealed class HTMLParser
{
    private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) MindWorkAIStudio/1.0";
    private const int MAX_REDIRECTS = 10;
    private const int DEFAULT_MAX_RESPONSE_BYTES = 5 * 1024 * 1024;

    /// <summary>
    /// The elements which ReverseMarkdown reads as inline elements around their content, such as a link.
    /// </summary>
    private static readonly FrozenSet<string> INLINE_ELEMENTS = new[] { "a", "abbr", "b", "cite", "del", "em", "i", "s", "span", "strike", "strong", "sub", "sup" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The elements which ReverseMarkdown reads as blocks of their own, or as containers of blocks. Tables and pre are
    /// left out: their content keeps its own structure inside an inline element as well.
    /// </summary>
    private static readonly FrozenSet<string> BLOCK_ELEMENTS = new[]
    {
        "article", "aside", "blockquote", "dd", "div", "dl", "dt", "figcaption", "figure", "footer", "h1", "h2", "h3", "h4", "h5", "h6",
        "header", "hr", "li", "main", "nav", "ol", "p", "section", "ul",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The one converter every HTML to Markdown conversion runs through.
    /// </summary>
    /// <remarks>
    /// Since version 6, ReverseMarkdown keeps the state of a conversion inside that conversion and
    /// documents its converter as safe for concurrent use. A web search converts up to four pages at
    /// the same time, and HTMLParserConcurrencyTests checks that they stay apart. Up to version 5, a
    /// converter tracked the ancestors of the node it was at in state of its own, and conversions
    /// sharing one tore each other's lists apart, which is why there used to be a pool of them.<br/><br/>
    /// Built from a configuration alone, the converter takes its built-in readers and needs no
    /// reflection, so a trimmed build keeps everything it uses, see MarkdownChecks of the trim smoke test.
    /// </remarks>
    private static readonly Converter CONVERTER = new(new Config
    {
        Tags = { Unknown = Config.UnknownTagsOption.Bypass },
        Formatting = { RemoveComments = true },
        Links = { SmartHref = true },
        Preprocess = { { "Unwrap the blocks inside inline elements", UnwrapBlocksInsideInlineElements } },
    });

    /// <summary>
    /// Loads a web page.
    /// </summary>
    /// <remarks>
    /// Callers go through the web page retrieval service rather than here: it decides which
    /// targets and which content types are acceptable, and extracts the readable content. This
    /// method only performs the request, and the validation it applies is the validation its
    /// caller hands in.<br/><br/>
    /// The media type is validated once the headers have arrived and before the body is read.
    /// A PDF of 30 MB is then refused for being a PDF, instead of being downloaded up to the
    /// size limit first and refused for its size.
    /// </remarks>
    public async Task<HTMLParserWebPage> LoadWebPageAsync(Uri url, int timeoutSeconds = 30,
        Func<Uri, CancellationToken, Task<IReadOnlyList<IPAddress>>>? resolveUrlAddressesAsync = null,
        int maxResponseBytes = DEFAULT_MAX_RESPONSE_BYTES, ExternalWebAuthenticationMode authenticationMode = ExternalWebAuthenticationMode.NONE,
        ExternalHttpTrustPolicy trustPolicy = ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED,
        Func<Uri, IReadOnlyList<IPAddress>, bool>? shouldUseDefaultCredentials = null, Action<string>? validateMediaType = null, CancellationToken token = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var cookieContainer = new CookieContainer();

        var currentUrl = url;
        for (var redirectCount = 0; redirectCount <= MAX_REDIRECTS; redirectCount++)
        {
            ValidateHttpOrHttpsUrl(currentUrl);
            var resolvedAddresses = resolveUrlAddressesAsync is null
                ? null
                : await resolveUrlAddressesAsync(currentUrl, timeoutCts.Token);
            var useDefaultCredentials = authenticationMode is ExternalWebAuthenticationMode.OS_DEFAULT_CREDENTIALS &&
                                        resolvedAddresses is not null &&
                                        shouldUseDefaultCredentials?.Invoke(currentUrl, resolvedAddresses) is true;
            using var handler = CreateHandler(currentUrl, resolvedAddresses, useDefaultCredentials, trustPolicy, cookieContainer);
            
            //
            // One client per redirect step, against the usual advice to keep them long-lived:
            // every step carries its own handler, and that handler is what makes this request
            // safe. It pins the connection to the IP addresses vetted for this exact URL, decides
            // whether the user's OS credentials may be sent, and applies the trust policy for this
            // host. A shared or pooled client would carry one of those decisions into a request it
            // was never made for. Socket exhaustion is not a concern here either: these requests
            // happen at human pace, one per web search result.
            //
            // ReSharper disable ShortLivedHttpClient
            using var httpClient = new HttpClient(handler);
            // ReSharper restore ShortLivedHttpClient

            // Set after the using declaration, so a throwing assignment still disposes the client.
            // The timeout is the caller's linked token instead, which also covers the redirects:
            httpClient.Timeout = Timeout.InfiniteTimeSpan;

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

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            validateMediaType?.Invoke(mediaType);

            var body = await HttpContentReader.ReadAsStringWithLimitAsync(response.Content, maxResponseBytes, timeoutCts.Token);
            return new HTMLParserWebPage
            {
                RequestedUrl = url,
                FinalUrl = response.RequestMessage?.RequestUri ?? currentUrl,
                ContentType = mediaType,
                Body = body,
            };
        }

        throw new HttpRequestException($"The server returned more than {MAX_REDIRECTS} redirects for '{url}'.");
    }

    private static SocketsHttpHandler CreateHandler(
        Uri url,
        IReadOnlyList<IPAddress>? resolvedAddresses,
        bool useDefaultCredentials,
        ExternalHttpTrustPolicy trustPolicy,
        CookieContainer cookieContainer)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect = false,
            UseCookies = true,
            CookieContainer = cookieContainer,
        };
        ExternalHttpClientTimeout.ConfigureSocketsHttpHandler(handler, url.Host, trustPolicy);

        if (useDefaultCredentials)
            handler.Credentials = CreateDefaultCredentialCache(url);

        if (resolvedAddresses is not null)
        {
            // The callback binds the request to a vetted target IP; a proxy would change the endpoint being connected to.
            handler.UseProxy = false;
            handler.ConnectCallback = (context, connectionToken) => ConnectToResolvedAddressAsync(context, resolvedAddresses, connectionToken);
        }

        return handler;
    }

    private static CredentialCache CreateDefaultCredentialCache(Uri url)
    {
        var credentialCache = new CredentialCache();
        var uriPrefix = new UriBuilder(url.Scheme, url.Host, url.Port).Uri;
        credentialCache.Add(uriPrefix, "Negotiate", CredentialCache.DefaultNetworkCredentials);
        credentialCache.Add(uriPrefix, "NTLM", CredentialCache.DefaultNetworkCredentials);
        credentialCache.Add(uriPrefix, "Kerberos", CredentialCache.DefaultNetworkCredentials);
        return credentialCache;
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
        IReadOnlyList<IPAddress> addresses,
        CancellationToken token)
    {
        var requestUri = context.InitialRequestMessage.RequestUri ??
                         throw new HttpRequestException("The HTTP request did not contain a target URL.");

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

        // What a browser asks for, too. A server offering a page still sends the page, while an
        // API that negotiates strictly answers with its JSON instead of refusing with a 406:
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
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



    public static string ExtractTitle(HtmlDocument document)
    {
        // HtmlAgilityPack annotates SelectSingleNode as never returning null, but a page without a
        // title element makes it do exactly that:
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        var title = document.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim();
        return WebUtility.HtmlDecode(title ?? string.Empty).Trim();
    }

    /// <summary>
    /// Converts HTML content to the Markdown format.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>The converted Markdown content.</returns>
    public static string ParseToMarkdown(string html) => CONVERTER.Convert(html);

    /// <summary>
    /// Replaces every block inside an inline element by its content, with a space before and after it.
    /// </summary>
    /// <param name="root">The document element of the HTML, as ReverseMarkdown parsed it.</param>
    /// <remarks>
    /// Compatibility shim, see documentation/compatibility-shims/2026-11-reversemarkdown-blocks-in-inline-elements.md;
    /// remove it once ReverseMarkdown separates such blocks itself.<br/><br/>
    /// Many pages wrap a whole teaser in a link: a heading, a line of metadata, and a summary, each a block of its own.
    /// A link holds no blocks in Markdown, so ReverseMarkdown 6 writes their texts one after another into the text of
    /// the link, without anything in between, e.g., "Java77.721 Abrufeheise security". Spaces next to such blocks alone
    /// were not enough: a first attempt which only added them still left most teasers on the start page of a news site
    /// glued together. Hence, each block gives way to its content, which the library then reads as part of the inline
    /// element, spaces included. The result is what the library makes of such a block anyway, only with the words
    /// apart. Where whitespace is there already, nothing is added, and inside pre the text stays exactly as it is.<br/><br/>
    /// The blocks are taken from the outside in, so an inner block sees the neighbors it got when its outer block gave
    /// way. Every conversion parses a document of its own, so this step changes nothing which another thread could see.
    /// </remarks>
    private static void UnwrapBlocksInsideInlineElements(IElement root)
    {
        var blocks = root.QuerySelectorAll("*").Where(IsBlockInsideInlineElement).ToList();
        foreach (var block in blocks)
        {
            if (block.Parent is not { } parent)
                continue;

            if (NeedsSeparator(block.PreviousSibling, atItsEnd: true))
                parent.InsertBefore(block.Owner!.CreateTextNode(" "), block);

            if (NeedsSeparator(block.NextSibling, atItsEnd: false))
                parent.InsertBefore(block.Owner!.CreateTextNode(" "), block.NextSibling);

            foreach (var child in block.ChildNodes.ToArray())
                parent.InsertBefore(child, block);

            block.Remove();
        }
    }

    private static bool IsBlockInsideInlineElement(IElement element)
    {
        if (!BLOCK_ELEMENTS.Contains(element.LocalName))
            return false;

        var insideInlineElement = false;
        for (var ancestor = element.ParentElement; ancestor is not null; ancestor = ancestor.ParentElement)
        {
            if (ancestor.LocalName.Equals("pre", StringComparison.OrdinalIgnoreCase))
                return false;

            if (INLINE_ELEMENTS.Contains(ancestor.LocalName))
                insideInlineElement = true;
        }

        return insideInlineElement;
    }

    /// <summary>
    /// Whether the node next to a block runs into the block without whitespace in between.
    /// </summary>
    /// <param name="neighbor">The node right before or after the block, if any.</param>
    /// <param name="atItsEnd">True when the neighbor comes before the block, so its last character touches the block.</param>
    private static bool NeedsSeparator(INode? neighbor, bool atItsEnd) => neighbor switch
    {
        null => false,
        IText { Data.Length: 0 } => false,
        IText text => !char.IsWhiteSpace(atItsEnd ? text.Data[^1] : text.Data[0]),
        _ => true,
    };
}