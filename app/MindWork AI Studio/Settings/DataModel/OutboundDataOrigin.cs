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
    /// A search of the configured Confluence wiki.
    /// </summary>
    CONFLUENCE_WIKI,

    /// <summary>
    /// An internal web page: one read from a private or otherwise non-public address, such as one
    /// in the intranet or behind a VPN, or a page of the configured wiki, whatever its address.
    /// </summary>
    INTERNAL_WEB_PAGE,
}