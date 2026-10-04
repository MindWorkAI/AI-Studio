using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;

namespace AIStudio.Tools.Web;

/// <summary>
/// Finds web addresses in text and tells whether two of them ask a server for the same thing.
/// </summary>
/// <remarks>
/// For a chat which may only read web pages whose addresses stand in it: an address counts as the
/// same when the request it makes is the same, because only the request leaves AI Studio.
/// </remarks>
public static partial class WebAddresses
{
    // Characters which end an address in running text. Parentheses and square brackets stay in, a
    // Wikipedia article has them in its address; an unbalanced one at the end is trimmed below:
    [GeneratedRegex("""https?://[^\s<>"'`{}|\\^]+""", RegexOptions.IgnoreCase)]
    private static partial Regex AddressPattern();

    private static readonly char[] TRAILING_PUNCTUATION = ['.', ',', ';', ':', '!', '?', '*', '_', '~'];

    /// <summary>
    /// The web addresses in a text, as they stand there.
    /// </summary>
    /// <remarks>
    /// Punctuation which ends a sentence is not part of an address, nor is the closing parenthesis
    /// of a Markdown link. An address written with HTML entities, as in a page whose links kept
    /// their &amp;amp;, comes as it stands and once more decoded.
    /// </remarks>
    /// <param name="text">The text to search.</param>
    /// <returns>The addresses found, possibly twice.</returns>
    public static IEnumerable<string> Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (Match match in AddressPattern().Matches(text))
        {
            var address = TrimEnd(match.Value);
            yield return address;

            var decoded = WebUtility.HtmlDecode(address);
            if (!string.Equals(decoded, address, StringComparison.Ordinal))
                yield return decoded;
        }
    }

    /// <summary>
    /// What a server gets asked for under this address, as a text to compare.
    /// </summary>
    /// <remarks>
    /// Scheme and host do not depend on case, so they are compared in lower case, and the host in
    /// its punycode form. Path and query are compared exactly, after the canonical form the request
    /// is sent in. The fragment never leaves the browser, so it does not count. User info does,
    /// because it can carry data as much as a query.
    /// </remarks>
    /// <param name="address">The address.</param>
    /// <param name="requestKey">The text to compare, when the address is an HTTP or HTTPS address.</param>
    /// <returns>True when the address is an HTTP or HTTPS address.</returns>
    public static bool TryCreateRequestKey(string? address, [NotNullWhen(true)] out string? requestKey)
    {
        requestKey = null;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
            return false;

        requestKey = CreateRequestKey(url);
        return true;
    }

    /// <summary>
    /// What a server gets asked for under this address, as a text to compare, see TryCreateRequestKey.
    /// </summary>
    /// <param name="url">The HTTP or HTTPS address.</param>
    /// <returns>The text to compare.</returns>
    public static string CreateRequestKey(Uri url)
    {
        var userInfo = url.GetComponents(UriComponents.UserInfo, UriFormat.UriEscaped);
        var host = WebHostHelper.Normalize(url.IdnHost);
        var pathAndQuery = url.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);
        return userInfo.Length > 0
            ? $"{url.Scheme}://{userInfo}@{host}:{url.Port}{pathAndQuery}"
            : $"{url.Scheme}://{host}:{url.Port}{pathAndQuery}";
    }

    private static string TrimEnd(string address)
    {
        while (address.Length > 0)
        {
            var last = address[^1];
            var isUnbalanced = last switch
            {
                ')' => address.Count(c => c is '(') < address.Count(c => c is ')'),
                ']' => address.Count(c => c is '[') < address.Count(c => c is ']'),
                _ => false,
            };

            if (!isUnbalanced && Array.IndexOf(TRAILING_PUNCTUATION, last) < 0)
                break;

            address = address[..^1];
        }

        return address;
    }
}