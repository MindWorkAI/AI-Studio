namespace AIStudio.Settings.DataModel;

/// <summary>
/// Where a chat may still send data, once internal content entered it: mails, a search of the
/// wiki, an internal web page, or passages of a data source.
/// </summary>
/// <remarks>
/// The members run from the strictest to the most permissive, so the stricter of two levels is the
/// smaller one. Services which are configured in AI Studio, such as Confluence, the allowed private
/// hosts, the data sources, or the mailbox itself, stay allowed on every level.
/// </remarks>
public enum OutboundDataRestriction
{
    // The strictest level is deliberately the member with the underlying value 0: when the settings
    // file holds a value TolerantEnumConverter cannot read, it falls back to that member. Falling back
    // to a more permissive level would open a way out for internal content the user meant to keep in.

    /// <summary>
    /// Only the services configured in AI Studio.
    /// </summary>
    ONLY_CONFIGURED_SERVICES = 0,

    /// <summary>
    /// The configured services, and the addresses written in the chat, either by the user or in the result of a tool. No searches with third-party services.
    /// </summary>
    ONLY_LINKS_FROM_CHAT,

    /// <summary>
    /// No restriction beyond the ones of each tool.
    /// </summary>
    UNRESTRICTED,
}