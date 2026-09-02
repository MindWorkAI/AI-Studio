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
            ExtractedPage = WebPageContentExtractor.Extract(page.Document, page.FinalUrl),
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

    private static async Task<IReadOnlyList<IPAddress>> ResolveValidatedUrlAddressesAsync(Uri url, WebPageRetrievalOptions options, CancellationToken token)
    {
        if (url is not { Scheme: "http" or "https" })
            throw new WebPageAccessBlockedException("Only HTTP and HTTPS URLs are supported.", WebPageAccessBlockReason.UNSUPPORTED_SCHEME);

        if (!options.TargetChosenByUser && IsBlockedHostName(url.Host))
            throw new WebPageAccessBlockedException("Local web page URLs are not supported.", WebPageAccessBlockReason.LOCAL_HOST_NAME);

        var addresses = await ResolveHostAddressesAsync(url, token);
        if (addresses.Count == 0)
            throw new InvalidOperationException($"The host '{url.Host}' did not resolve to an IP address.");

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

        if (options.ProviderConfidence >= ConfidenceLevel.HIGH || options.ProviderIsTrustedByConfiguration)
            return addresses;

        if (options.OnPrivateHostProviderBlockAsync is not null)
            await options.OnPrivateHostProviderBlockAsync(url, options.ProviderConfidence);
        throw new WebPageAccessBlockedException("This private or VPN web page requires a High-confidence provider or a provider trusted by configuration.", WebPageAccessBlockReason.INSUFFICIENT_PROVIDER_CONFIDENCE);
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

    private static bool IsSupportedHtmlContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ||
        contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
}

public sealed class WebPageRetrievalOptions
{
    public required int TimeoutSeconds { get; init; }

    /// <summary>
    /// Whether the user named this exact URL, as opposed to a model asking for it.
    /// </summary>
    /// <remarks>
    /// Lifts the restrictions on which targets may be reached — private networks, loopback, and
    /// hosts named localhost — because those exist to keep a model from reaching into the user's
    /// network, and the user is not a model. The network-level protections stay: the connection
    /// is still bound to validated addresses, redirects are still checked, the response size is
    /// still capped, and only HTML is still accepted.<br/><br/>
    /// Never set this for a URL that reached AI Studio through a model, however plausible it
    /// looks.
    /// </remarks>
    public bool TargetChosenByUser { get; init; }

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
