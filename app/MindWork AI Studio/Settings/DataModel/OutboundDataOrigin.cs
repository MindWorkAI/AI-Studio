namespace AIStudio.Settings.DataModel;

/// <summary>
/// What kind of source brought the internal content into a chat which restricts where it may
/// still send data.
/// </summary>
public enum OutboundDataOrigin
{
    /// <summary>
    /// No source: the chat holds no internal content, so nothing restricts it.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// A configured mailbox.
    /// </summary>
    MAILBOX,

    /// <summary>
    /// A search of the configured Confluence wiki.
    /// </summary>
    CONFLUENCE_WIKI,

    /// <summary>
    /// An internal web page: one read from a private or otherwise non-public address, such as one
    /// in the intranet or behind a VPN, or a page of the configured wiki, whatever its address.
    /// </summary>
    INTERNAL_WEB_PAGE,

    /// <summary>
    /// A search of the configured intranet.
    /// </summary>
    INTRANET,
}