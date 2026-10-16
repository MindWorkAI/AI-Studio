namespace AIStudio.Settings.DataModel;

/// <summary>
/// What kind of source brought the internal content into a chat which restricts where it may
/// still send data.
/// </summary>
public enum OutboundDataOrigin
{
    // Every requirement saved before this enum existed came from a mailbox. It is therefore the
    // member with the underlying value 0, which a chat saved without an origin reads as.

    /// <summary>
    /// A configured mailbox.
    /// </summary>
    MAILBOX = 0,

    /// <summary>
    /// The configured Confluence wiki.
    /// </summary>
    CONFLUENCE_WIKI,

    /// <summary>
    /// A web page on a private or otherwise non-public host, such as one in the intranet or behind a VPN.
    /// </summary>
    PRIVATE_WEB_PAGE,
}