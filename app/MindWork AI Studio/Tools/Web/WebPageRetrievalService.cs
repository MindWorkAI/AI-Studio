using System.Net;
using System.Net.Sockets;
using AIStudio.Provider;
using HtmlAgilityPack;

namespace AIStudio.Tools.Web;

/// <summary>
/// Reads web pages, text documents, and, where the caller allows it, documents such as PDFs.
/// </summary>
/// <param name="htmlParser">Performs the requests.</param>
/// <param name="documentReader">Reads documents through the runtime, or null where no document is ever read.</param>
public sealed class WebPageRetrievalService(HTMLParser htmlParser, WebDocumentReader? documentReader = null)
{
    private const int MAX_RESPONSE_BYTES = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// Reads the page or document at an address.
    /// </summary>
    /// <remarks>
    /// A document is read only when the options allow documents for the address which returned
    /// it, see WebDocumentOptions. It streams into a file of its own, which the runtime reads and
    /// which is deleted right after, whether reading succeeded, failed, or was cancelled. Whether
    /// a response counts as a document is decided by its media type, as everything else is read
    /// as HTML or text; what kind of document it is, the runtime decides by its content.
    /// </remarks>
    public async Task<RetrievedWebPage> RetrieveAsync(
        Uri url,
        WebPageRetrievalOptions options,
        CancellationToken token = default)
    {
        string? documentPath = null;
        try
        {
            return await this.RetrieveCoreAsync(url, options, path => documentPath = path, token);
        }
        finally
        {
            if (documentPath is not null)
                documentReader?.Delete(documentPath);
        }
    }

    private async Task<RetrievedWebPage> RetrieveCoreAsync(Uri url, WebPageRetrievalOptions options, Action<string> rememberDocumentPath, CancellationToken token)
    {
        var triedOsSso = false;
        var requiredProviderConfidence = ConfidenceLevel.NONE;
        var lastAddressIsNonPublic = false;

        // Always overwritten: the media type is validated before the body is read, so no page
        // arrives without the check having decided what it is.
        var contentKind = WebContentKind.HTML_PAGE;
        var documentDownload = options.Documents is { } documents && documentReader is not null
            ? new HTMLParserDocumentDownload((mediaType, finalUrl) =>
            {
                // Pages and text formats are read as before, wherever they come from, and images,
                // audio, or video are refused by the media type check before their body is downloaded:
                if (WebContentTypeClassifier.Classify(mediaType) is not null || !MayBeDocument(mediaType))
                    return null;

                // The text names no address, so a caller may hand it on without repeating one:
                if (!documents.IsAllowed(finalUrl, lastAddressIsNonPublic))
                    throw new WebPageAccessBlockedException("The address returned a document, such as a PDF, which may not be read from there.", WebPageAccessBlockReason.DOCUMENT_NOT_ALLOWED);

                var path = documentReader.CreatePath();
                rememberDocumentPath(path);
                return path;
            }, documents.MaxBytes)
            : null;

        HTMLParserWebPage page;
        try
        {
            page = await htmlParser.LoadWebPageAsync(
                url,
                options.TimeoutSeconds,
                async (candidateUrl, validationToken) =>
                {
                    var addresses = await ResolveValidatedUrlAddressesAsync(candidateUrl, options, validationToken);
                    // Overwritten with every request, so it holds what the last one reached, the
                    // one which returned the page:
                    lastAddressIsNonPublic = addresses.Any(IsNonPublicAddress);
                    if (lastAddressIsNonPublic)
                        requiredProviderConfidence = ConfidenceLevel.HIGH;

                    return addresses;
                },
                MAX_RESPONSE_BYTES,
                options.UseOsSso ? ExternalWebAuthenticationMode.OS_DEFAULT_CREDENTIALS : ExternalWebAuthenticationMode.NONE,
                shouldUseDefaultCredentials: (candidateUrl, addresses) =>
                {
                    var shouldTryOsSso = ShouldTryOsSso(url, candidateUrl, addresses, options);
                    triedOsSso |= shouldTryOsSso;
                    return shouldTryOsSso;
                },
                validateMediaType: mediaType => contentKind = WebContentTypeClassifier.Classify(mediaType) ??
                    throw new InvalidOperationException($"Unsupported content type '{mediaType}'. Only HTML pages and text formats such as plain text, JSON, XML, or CSV are supported."),
                documentDownload: documentDownload,
                token: token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException($"Loading the web page timed out after {options.TimeoutSeconds} seconds.");
        }
        catch (HttpRequestException exception)
        {
            if (FindBlockedException(exception) is { } blockedException)
                throw blockedException;

            if (triedOsSso && exception.StatusCode is HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException(
                    $"Loading the web page failed: The server returned HTTP 401 (Unauthorized) for '{url}'. The host is reachable and AI Studio already tried your operating system's default sign-in, but the server did not accept it or requires an additional browser session/cookies.",
                    exception);
            }

            throw new InvalidOperationException($"Loading the web page failed: {exception.Message}", exception);
        }

        if (page.DocumentPath is { } downloadedPath && documentReader is not null && options.Documents is not null)
            return await ReadDocumentAsync(documentReader, page, downloadedPath, options.Documents, requiredProviderConfidence, lastAddressIsNonPublic, token);

        return new RetrievedWebPage
        {
            Page = page,
            ContentKind = contentKind,
            ExtractedPage = contentKind switch
            {
                WebContentKind.TEXT_DOCUMENT => WebTextContentExtractor.Extract(page.Body, page.ContentType, page.FinalUrl),
                _ => WebPageContentExtractor.Extract(ParseHtml(page.Body), page.FinalUrl),
            },
            RetrievedAtUtc = DateTimeOffset.UtcNow,
            RequiredProviderConfidence = requiredProviderConfidence,
            ReachedNonPublicAddress = lastAddressIsNonPublic,
        };
    }

    private static async Task<RetrievedWebPage> ReadDocumentAsync(WebDocumentReader reader, HTMLParserWebPage page, string path, WebDocumentOptions documents, ConfidenceLevel requiredProviderConfidence, bool reachedNonPublicAddress, CancellationToken token)
    {
        var extraction = await reader.ReadAsync(path, page.FinalUrl, documents.MaxCharacters, token);
        if (!extraction.HasUsableContent)
        {
            token.ThrowIfCancellationRequested();

            // English, like every other failure here, because it reaches the model:
            throw new InvalidOperationException(extraction.ErrorCode switch
            {
                FileExtractionErrorCode.UNSUPPORTED when extraction.DetectedFormat is not null => $"The document is a {extraction.DetectedFormat}, which AI Studio cannot read.",
                FileExtractionErrorCode.UNSUPPORTED => "The document has a format which AI Studio cannot read.",
                FileExtractionErrorCode.EXECUTABLE_REJECTED => "The address returned a program instead of a document, which is never read.",
                FileExtractionErrorCode.PDF_ENCRYPTED => "The document is protected and cannot be opened.",
                FileExtractionErrorCode.NO_TEXT_EXTRACTED or FileExtractionErrorCode.NO_CONTENT => "No text could be read from the document. It might contain images only, such as a scanned PDF without a text layer.",
                _ => $"Reading the document failed: {extraction.ErrorMessage}",
            });
        }

        return new RetrievedWebPage
        {
            Page = page,
            ContentKind = WebContentKind.DOCUMENT,
            ExtractedPage = new ExtractedWebPage
            {
                Title = page.FileName ?? GetFileName(page.FinalUrl),
                Description = string.Empty,
                Authors = [],
                PublishedTime = string.Empty,
                ModifiedTime = string.Empty,
                Language = string.Empty,
                SiteName = string.Empty,
                CanonicalUrl = null,
                Markdown = extraction.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim(),
                Outline = [],
            },
            RetrievedAtUtc = DateTimeOffset.UtcNow,
            RequiredProviderConfidence = requiredProviderConfidence,
            ReachedNonPublicAddress = reachedNonPublicAddress,
            IsCutAtCharacterLimit = extraction.IsCutAtCharacterLimit,
            HasUnreadableParts = extraction.Outcome is FileExtractionOutcome.PARTIAL,
        };
    }

    /// <summary>
    /// Whether a response of this media type may hold a document such as a PDF.
    /// </summary>
    /// <remarks>
    /// Documents arrive as application/*, often as application/octet-stream, and sometimes without
    /// any media type. The runtime decides by the content which of them it reads. An image, a
    /// recording, or a video is none, and downloading it first would cost up to the size limit of
    /// documents before the runtime refused it.
    /// </remarks>
    private static bool MayBeDocument(string mediaType) =>
        mediaType.Length is 0 || mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase);

    private static string GetFileName(Uri url)
    {
        var fileName = Path.GetFileName(Uri.UnescapeDataString(url.AbsolutePath));
        return string.IsNullOrWhiteSpace(fileName) ? url.Host : fileName;
    }

    private static HtmlDocument ParseHtml(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document;
    }

    private static WebPageAccessBlockedException? FindBlockedException(Exception exception)
    {
        if (exception is WebPageAccessBlockedException blockedException)
            return blockedException;

        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                if (FindBlockedException(innerException) is { } innerBlockedException)
                    return innerBlockedException;
            }
        }

        return exception.InnerException is null ? null : FindBlockedException(exception.InnerException);
    }

    private static async Task<IReadOnlyList<IPAddress>> ResolveValidatedUrlAddressesAsync(Uri url, WebPageRetrievalOptions options, CancellationToken token)
    {
        if (url is not { Scheme: "http" or "https" })
            throw new WebPageAccessBlockedException("Only HTTP and HTTPS URLs are supported.", WebPageAccessBlockReason.UNSUPPORTED_SCHEME);

        if (options.IsTargetAllowed?.Invoke(url) is false)
            throw new WebPageAccessBlockedException($"The web page '{url.GetLeftPart(UriPartial.Path)}' is outside the targets this request may reach.", WebPageAccessBlockReason.TARGET_NOT_ALLOWED);

        if (!options.TargetChosenByUser && IsBlockedHostName(url.Host))
            throw new WebPageAccessBlockedException("Local web page URLs are not supported.", WebPageAccessBlockReason.LOCAL_HOST_NAME);

        var addresses = await ResolveHostAddressesAsync(url, token);
        if (addresses.Count == 0)
            throw new InvalidOperationException($"The host '{url.Host}' did not resolve to an IP address.");

        // Before the exemption for targets the user chose, because it guards what the address may
        // carry. Every address has to be non-public, since the connection may go to any of them.
        // The text names no address, so a caller may hand it on without repeating one:
        if (options.NonPublicTargetsOnly && !addresses.All(IsNonPublicAddress))
            throw new WebPageAccessBlockedException("The web page or one of its redirects leads to a public address, which this request may not reach.", WebPageAccessBlockReason.PUBLIC_TARGET_NOT_ALLOWED);

        //
        // Where the target came from decides which targets are acceptable. A URL a model produced
        // may not reach into the local network, because the model was talked into it by whatever
        // it read. A URL the user typed carries no such doubt: it is their machine and their
        // network, and refusing an internal wiki or a local server would only be in their way.
        //
        // What stays in force either way is everything protecting against a URL leading somewhere
        // other than where it appears to: the connection is bound to the addresses validated
        // here, and every redirect passes through this method again.
        //
        if (options.TargetChosenByUser)
            return addresses;

        if (addresses.Any(IsNeverAllowedAddress))
            throw new WebPageAccessBlockedException("Local, link-local, multicast, and unspecified network addresses are not supported.", WebPageAccessBlockReason.NEVER_ALLOWED_ADDRESS);

        if (!addresses.Any(IsNonPublicAddress))
            return addresses;

        if (options.PublicTargetsOnly || options.IsPrivateHostAllowed?.Invoke(url.Host) is not true)
            throw new WebPageAccessBlockedException("Private or local-network web page URLs are not supported unless their host is explicitly allowed.", WebPageAccessBlockReason.PRIVATE_HOST_NOT_ALLOWED);

        if (options.ProviderConfidence >= ConfidenceLevel.HIGH)
            return addresses;

        if (options.OnPrivateHostProviderBlockAsync is not null)
            await options.OnPrivateHostProviderBlockAsync(url, options.ProviderConfidence);
        throw new WebPageAccessBlockedException("This private or VPN web page requires a High-confidence provider.", WebPageAccessBlockReason.INSUFFICIENT_PROVIDER_CONFIDENCE);
    }

    private static async Task<IReadOnlyList<IPAddress>> ResolveHostAddressesAsync(Uri url, CancellationToken token)
    {
        if (IPAddress.TryParse(url.Host, out var parsedAddress))
            return [NormalizeAddress(parsedAddress)];

        try
        {
            return (await Dns.GetHostAddressesAsync(url.DnsSafeHost, token))
                .Select(NormalizeAddress)
                .ToList();
        }
        catch (SocketException exception)
        {
            throw new InvalidOperationException($"The host '{url.Host}' could not be resolved: {exception.Message}", exception);
        }
    }

    private static bool ShouldTryOsSso(
        Uri originalUrl,
        Uri candidateUrl,
        IReadOnlyList<IPAddress> addresses,
        WebPageRetrievalOptions options) =>
        options is { UseOsSso: true, ProviderConfidence: >= ConfidenceLevel.HIGH } &&
        candidateUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        originalUrl.Scheme.Equals(candidateUrl.Scheme, StringComparison.OrdinalIgnoreCase) &&
        originalUrl.Host.Equals(candidateUrl.Host, StringComparison.OrdinalIgnoreCase) &&
        originalUrl.Port == candidateUrl.Port &&
        !IsBlockedHostName(candidateUrl.Host) &&
        options.IsPrivateHostAllowed?.Invoke(candidateUrl.Host) is true &&
        addresses.Count > 0 &&
        addresses.All(IsNonPublicAddress);

    private static IPAddress NormalizeAddress(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static bool IsBlockedHostName(string host)
    {
        var normalizedHost = WebHostHelper.Normalize(host);
        return normalizedHost is "localhost" ||
               normalizedHost.EndsWith(".localhost", StringComparison.Ordinal);
    }

    private static bool IsNeverAllowedAddress(IPAddress address)
    {
        address = NormalizeAddress(address);
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily is AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return address.Equals(IPAddress.Any) ||
                   bytes[0] is 0 or 127 or >= 224 ||
                   (bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily is AddressFamily.InterNetworkV6)
        {
            if (address.Equals(IPAddress.IPv6Any) ||
                address.Equals(IPAddress.IPv6None) ||
                address.Equals(IPAddress.IPv6Loopback) ||
                address.IsIPv6LinkLocal ||
                address.IsIPv6Multicast)
                return true;

            // Checked here as well as among the non-public addresses, because an embedded
            // loopback or link-local address must stay refused outright rather than become
            // something an allowlist can permit:
            return TryGetEmbeddedIPv4Address(address) is { } embeddedAddress && IsNeverAllowedAddress(embeddedAddress);
        }

        return true;
    }

    private static bool IsNonPublicAddress(IPAddress address)
    {
        address = NormalizeAddress(address);
        if (IsNeverAllowedAddress(address))
            return true;

        if (address.AddressFamily is AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) ||
                   (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
                   (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                   (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
                   (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        }

        if (address.AddressFamily is AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xfe) == 0xfc || address.IsIPv6SiteLocal)
                return true;

            return TryGetEmbeddedIPv4Address(address) is { } embeddedAddress && IsNonPublicAddress(embeddedAddress);
        }

        return true;
    }

    /// <summary>
    /// Reads the IPv4 address an IPv6 address carries inside it, if it does.
    /// </summary>
    /// <remarks>
    /// Several transition mechanisms embed an IPv4 address in an IPv6 one. Judged by their IPv6
    /// form alone, they all look like ordinary public addresses, so <c>64:ff9b::10.0.0.1</c> would
    /// reach the local network that plain <c>10.0.0.1</c> is refused for.<br/><br/>
    /// The address is only read, never replaced: the connection has to go to the IPv6 address as
    /// resolved, because the embedded IPv4 address is reached through a gateway rather than
    /// directly. Only the judgement about it uses what is inside.
    /// </remarks>
    private static IPAddress? TryGetEmbeddedIPv4Address(IPAddress address)
    {
        if (address.AddressFamily is not AddressFamily.InterNetworkV6)
            return null;

        var bytes = address.GetAddressBytes();

        // NAT64 well-known prefix 64:ff9b::/96 — the last four bytes are the IPv4 address:
        if (bytes[0] is 0x00 && bytes[1] is 0x64 && bytes[2] is 0xff && bytes[3] is 0x9b &&
            bytes[4..12].All(part => part is 0x00))
            return new IPAddress(bytes[12..16]);

        // 6to4 2002::/16 — the IPv4 address follows the prefix:
        if (bytes[0] is 0x20 && bytes[1] is 0x02)
            return new IPAddress(bytes[2..6]);

        // Teredo 2001:0000::/32 — the client's IPv4 address sits at the end, bitwise inverted:
        if (bytes[0] is 0x20 && bytes[1] is 0x01 && bytes[2] is 0x00 && bytes[3] is 0x00)
            return new IPAddress(bytes[12..16].Select(part => (byte)~part).ToArray());

        return null;
    }
}