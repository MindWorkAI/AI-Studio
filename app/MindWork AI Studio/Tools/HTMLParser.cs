using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using AIStudio.Tools.Web;
using HtmlAgilityPack;
using ReverseMarkdown;

namespace AIStudio.Tools;

public sealed class HTMLParser
{
    private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) MindWorkAIStudio/1.0";
    private const int MAX_REDIRECTS = 10;
    private const int DEFAULT_MAX_RESPONSE_BYTES = 5 * 1024 * 1024;

    /// <summary>
    /// The HTML to Markdown converter, built once from a fixed configuration.
    /// </summary>
    /// <remarks>
    /// Shared rather than built per call: the configuration never changes, and one web search
    /// converts a page per result.
    /// </remarks>
    private static readonly Converter MARKDOWN_CONVERTER = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.Bypass,
        RemoveComments = true,
        SmartHrefHandling = true,
    });

    /// <summary>
    /// Loads a web page.
    /// </summary>
    /// <remarks>
    /// Callers go through the web page retrieval service rather than here: it decides which
    /// targets are acceptable and extracts the readable content. This method only performs the
    /// request, and the validation it applies is the validation its caller hands in.
    /// </remarks>
    public async Task<HTMLParserWebPage> LoadWebPageAsync(Uri url, CancellationToken token = default, int timeoutSeconds = 30,
        Func<Uri, CancellationToken, Task<IReadOnlyList<IPAddress>>>? resolveUrlAddressesAsync = null,
        int maxResponseBytes = DEFAULT_MAX_RESPONSE_BYTES, ExternalWebAuthenticationMode authenticationMode = ExternalWebAuthenticationMode.NONE,
        ExternalHttpTrustPolicy trustPolicy = ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED,
        Func<Uri, IReadOnlyList<IPAddress>, bool>? shouldUseDefaultCredentials = null)
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
            using var httpClient = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };

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

            var html = await HttpContentReader.ReadAsStringWithLimitAsync(response.Content, maxResponseBytes, timeoutCts.Token);
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
        var title = document.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
        return WebUtility.HtmlDecode(title ?? string.Empty).Trim();
    }

    /// <summary>
    /// Converts HTML content to the Markdown format.
    /// </summary>
    /// <param name="html">The HTML content to parse.</param>
    /// <returns>The converted Markdown content.</returns>
    public static string ParseToMarkdown(string html) => MARKDOWN_CONVERTER.Convert(html);
}

public sealed class HTMLParserWebPage
{
    public required Uri RequestedUrl { get; init; }

    public required Uri FinalUrl { get; init; }

    public required string ContentType { get; init; }

    public required HtmlDocument Document { get; init; }
}

public enum ExternalWebAuthenticationMode
{
    NONE,
    OS_DEFAULT_CREDENTIALS
}
