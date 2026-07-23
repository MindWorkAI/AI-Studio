using System.Net;
using System.Net.Sockets;
using AIStudio.Provider;

namespace AIStudio.Tools.Web;

public sealed class WebPageRetrievalService(HTMLParser htmlParser)
{
    private const int MAX_RESPONSE_BYTES = 5 * 1024 * 1024; // 5MB

    public async Task<RetrievedWebPage> RetrieveAsync(
        Uri url,
        WebPageRetrievalOptions options,
        CancellationToken token = default)
    {
        var triedOsSso = false;
        var requiredProviderConfidence = ConfidenceLevel.NONE;
        HTMLParserWebPage page;
        try
        {
            page = await htmlParser.LoadWebPageAsync(
                url,
                token,
                options.TimeoutSeconds,
                async (candidateUrl, validationToken) =>
                {
                    var addresses = await ResolveValidatedUrlAddressesAsync(candidateUrl, options, validationToken);
                    if (addresses.Any(IsNonPublicAddress))
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
                });
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

        if (!IsSupportedHtmlContentType(page.ContentType))
            throw new InvalidOperationException($"Unsupported content type '{page.ContentType}'. Only HTML pages are supported.");

        return new RetrievedWebPage
        {
            Page = page,
            ExtractedPage = WebPageContentExtractor.Extract(htmlParser, page.Document, page.FinalUrl),
            RetrievedAtUtc = DateTimeOffset.UtcNow,
            RequiredProviderConfidence = requiredProviderConfidence,
        };
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

    private static async Task<IReadOnlyList<IPAddress>> ResolveValidatedUrlAddressesAsync(
        Uri url,
        WebPageRetrievalOptions options,
        CancellationToken token)
    {
        if (url is not { Scheme: "http" or "https" })
            throw new WebPageAccessBlockedException(
                "Only HTTP and HTTPS URLs are supported.",
                WebPageAccessBlockReason.UNSUPPORTED_SCHEME);

        if (IsBlockedHostName(url.Host))
            throw new WebPageAccessBlockedException(
                "Local web page URLs are not supported.",
                WebPageAccessBlockReason.LOCAL_HOST_NAME);

        var addresses = await ResolveHostAddressesAsync(url, token);
        if (addresses.Count == 0)
            throw new InvalidOperationException($"The host '{url.Host}' did not resolve to an IP address.");

        if (addresses.Any(IsNeverAllowedAddress))
            throw new WebPageAccessBlockedException(
                "Local, link-local, multicast, and unspecified network addresses are not supported.",
                WebPageAccessBlockReason.NEVER_ALLOWED_ADDRESS);

        if (!addresses.Any(IsNonPublicAddress))
            return addresses;

        if (options.PublicTargetsOnly || options.IsPrivateHostAllowed?.Invoke(url.Host) is not true)
            throw new WebPageAccessBlockedException(
                "Private or local-network web page URLs are not supported unless their host is explicitly allowed.",
                WebPageAccessBlockReason.PRIVATE_HOST_NOT_ALLOWED);

        if (options.ProviderConfidence >= ConfidenceLevel.HIGH || options.ProviderIsTrustedByConfiguration)
            return addresses;

        if (options.OnPrivateHostProviderBlockAsync is not null)
            await options.OnPrivateHostProviderBlockAsync(url, options.ProviderConfidence);
        throw new WebPageAccessBlockedException(
            "This private or VPN web page requires a High-confidence provider or a provider trusted by configuration.",
            WebPageAccessBlockReason.INSUFFICIENT_PROVIDER_CONFIDENCE);
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
        options.UseOsSso &&
        (options.ProviderConfidence >= ConfidenceLevel.HIGH || options.ProviderIsTrustedByConfiguration) &&
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
            return address.Equals(IPAddress.IPv6Any) ||
                   address.Equals(IPAddress.IPv6None) ||
                   address.Equals(IPAddress.IPv6Loopback) ||
                   address.IsIPv6LinkLocal ||
                   address.IsIPv6Multicast;
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
            return (bytes[0] & 0xfe) == 0xfc ||
                   address.IsIPv6SiteLocal;
        }

        return true;
    }

    private static bool IsSupportedHtmlContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ||
        contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
}

public sealed class WebPageRetrievalOptions
{
    public required int TimeoutSeconds { get; init; }

    public bool PublicTargetsOnly { get; init; }

    public ConfidenceLevel ProviderConfidence { get; init; } = ConfidenceLevel.NONE;

    public bool ProviderIsTrustedByConfiguration { get; init; }

    public bool UseOsSso { get; init; }

    public Func<string, bool>? IsPrivateHostAllowed { get; init; }

    public Func<Uri, ConfidenceLevel, Task>? OnPrivateHostProviderBlockAsync { get; init; }
}

public sealed class RetrievedWebPage
{
    public required HTMLParserWebPage Page { get; init; }

    public required ExtractedWebPage ExtractedPage { get; init; }

    public required DateTimeOffset RetrievedAtUtc { get; init; }

    public ConfidenceLevel RequiredProviderConfidence { get; init; } = ConfidenceLevel.NONE;
}
