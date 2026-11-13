using System.Diagnostics.CodeAnalysis;

using AIStudio.Tools.Services;

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

    /// <summary>
    /// Checks whether a text is one of the links which the Assistant Builder writes into the plugins it
    /// generates.
    /// </summary>
    /// <remarks>
    /// Every plugin has to state a support contact and a source URL, so the Assistant Builder fills in
    /// links to MindWork AI. They tell nothing about the plugin itself, so AI Studio treats them as not
    /// stated: it neither shows them nor offers a button for them. The check goes by the text alone, so
    /// these links count as not stated in any other plugin as well.
    /// </remarks>
    /// <param name="text">The text to check. Whitespace around it does not count.</param>
    /// <returns>True, when the text is one of these placeholders.</returns>
    public static bool IsPlaceholder(string text) => text.Trim() is AssistantPluginGenerationService.DEFAULT_SUPPORT_CONTACT or AssistantPluginGenerationService.DEFAULT_SOURCE_URL;

    /// <summary>
    /// Gets the text by which the user recognizes a link.
    /// </summary>
    /// <param name="uri">The link.</param>
    /// <returns>For a mailto link its recipient, without the scheme and without a subject. For a website the link as the plugin states it.</returns>
    public static string GetDisplayText(Uri uri) => IsMail(uri) ? Uri.UnescapeDataString(GetRecipient(uri).Trim().ToString()) : uri.OriginalString;

    private static bool IsAllowed(Uri uri)
    {
        // Without a recipient, the mail program would open a mail to nobody:
        if (IsMail(uri))
            return !GetRecipient(uri).IsWhiteSpace();

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the recipient of a mailto link, which is everything between the colon and the first
    /// question mark.
    /// </summary>
    /// <param name="mailUri">The mailto link.</param>
    /// <returns>The recipient as the link states it, which may be empty.</returns>
    private static ReadOnlySpan<char> GetRecipient(Uri mailUri)
    {
        var text = mailUri.OriginalString.AsSpan();
        var recipient = text[(text.IndexOf(':') + 1)..];
        var queryStart = recipient.IndexOf('?');
        return queryStart >= 0 ? recipient[..queryStart] : recipient;
    }
}