using System.Diagnostics.CodeAnalysis;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Recognizes the links in the metadata of plugins.
/// </summary>
/// <remarks>
/// The source URL of a plugin has to be such a link, and its support contact may be one. A plugin may
/// only link to a website or to a mail address: another scheme, such as file: or javascript:, would let a
/// plugin open anything on the computer of the user with a single click. Loading a plugin and showing its
/// links both decide with this class, so that AI Studio never offers a link which loading would refuse.
/// </remarks>
public static class PluginLinks
{
    /// <summary>
    /// Tries to read a text as a link which a plugin may offer.
    /// </summary>
    /// <param name="text">The text to read. Whitespace around it does not count.</param>
    /// <param name="uri">The link, when the text is one.</param>
    /// <returns>True, when the text is an absolute http, https, or mailto link, and a mailto link names a recipient.</returns>
    public static bool TryParse(string text, [NotNullWhen(true)] out Uri? uri)
    {
        if (Uri.TryCreate(text.Trim(), UriKind.Absolute, out uri) && IsAllowed(uri))
            return true;

        uri = null;
        return false;
    }

    /// <summary>
    /// Checks whether a link writes a mail instead of opening a website.
    /// </summary>
    /// <param name="uri">The link to check.</param>
    /// <returns>True, when the link is a mailto link.</returns>
    public static bool IsMail(Uri uri) => uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowed(Uri uri)
    {
        if (IsMail(uri))
            return HasRecipient(uri);

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether a mailto link names a recipient, which is everything between the colon and the
    /// first question mark. Without one, the mail program would open a mail to nobody.
    /// </summary>
    /// <param name="mailUri">The mailto link to check.</param>
    /// <returns>True, when the link names a recipient.</returns>
    private static bool HasRecipient(Uri mailUri)
    {
        var text = mailUri.OriginalString.AsSpan();
        var recipient = text[(text.IndexOf(':') + 1)..];
        var queryStart = recipient.IndexOf('?');
        if (queryStart >= 0)
            recipient = recipient[..queryStart];

        return !recipient.IsWhiteSpace();
    }
}