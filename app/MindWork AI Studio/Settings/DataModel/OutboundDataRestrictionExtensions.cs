using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class OutboundDataRestrictionExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(OutboundDataRestrictionExtensions).Namespace, nameof(OutboundDataRestrictionExtensions));

    /// <summary>
    /// The stricter of two restrictions.
    /// </summary>
    /// <remarks>
    /// A restriction this version does not know counts as the strictest one, see OrStrictestWhenUnknown.
    /// </remarks>
    /// <param name="restriction">The one restriction.</param>
    /// <param name="other">The other restriction.</param>
    /// <returns>The restriction which allows less.</returns>
    public static OutboundDataRestriction StricterOf(this OutboundDataRestriction restriction, OutboundDataRestriction other)
    {
        var knownRestriction = restriction.OrStrictestWhenUnknown();
        var knownOther = other.OrStrictestWhenUnknown();
        return knownOther < knownRestriction ? knownOther : knownRestriction;
    }

    /// <summary>
    /// The restriction as this version applies it.
    /// </summary>
    /// <remarks>
    /// The settings file and a saved chat may hold a number which names no member, e.g., one written
    /// by hand, and the settings read it all the same. Compared as it is, such a number would rank
    /// above UNRESTRICTED and lift every restriction it meets.
    /// </remarks>
    /// <param name="restriction">The restriction as it was read.</param>
    /// <returns>The restriction itself, or the strictest one when this version does not know it.</returns>
    public static OutboundDataRestriction OrStrictestWhenUnknown(this OutboundDataRestriction restriction) => Enum.IsDefined(restriction) ? restriction : OutboundDataRestriction.ONLY_CONFIGURED_SERVICES;

    public static string GetName(this OutboundDataRestriction restriction) => restriction switch
    {
        OutboundDataRestriction.ONLY_CONFIGURED_SERVICES => TB("Only services configured in AI Studio"),
        OutboundDataRestriction.ONLY_LINKS_FROM_CHAT => TB("Configured services and addresses from the chat"),
        OutboundDataRestriction.UNRESTRICTED => TB("No restriction"),

        _ => TB("Unknown restriction"),
    };

    /// <summary>
    /// What a chat may still do on this level, once internal content entered it.
    /// </summary>
    public static string GetDescription(this OutboundDataRestriction restriction) => restriction switch
    {
        OutboundDataRestriction.ONLY_CONFIGURED_SERVICES => TB("The chat may only use services configured in AI Studio: your mailboxes, your data sources, your Confluence wiki, and the allowed private hosts of the tool 'Read Web Page'. It reads no other web pages, and it does not search the web."),
        OutboundDataRestriction.ONLY_LINKS_FROM_CHAT => TB("The chat may also read web pages whose addresses stand in the chat, written by you or returned by a tool, exactly as they stand there. It does not search the web, and the AI cannot choose addresses of its own outside the services configured in AI Studio."),
        OutboundDataRestriction.UNRESTRICTED => TB("The chat may use every tool you chose, web search and any web page included. Internal content, such as your mails or the documents of your data sources, can then reach third parties, e.g., inside a search query or the address of a web page."),

        _ => TB("This version of AI Studio does not know this restriction, so it applies the strictest one."),
    };
}