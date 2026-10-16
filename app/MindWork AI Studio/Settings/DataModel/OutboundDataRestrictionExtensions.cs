using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class OutboundDataRestrictionExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(OutboundDataRestrictionExtensions).Namespace, nameof(OutboundDataRestrictionExtensions));

    /// <summary>
    /// The stricter of two restrictions.
    /// </summary>
    /// <param name="restriction">The one restriction.</param>
    /// <param name="other">The other restriction.</param>
    /// <returns>The restriction which allows less.</returns>
    public static OutboundDataRestriction StricterOf(this OutboundDataRestriction restriction, OutboundDataRestriction other) => other < restriction ? other : restriction;

    public static string GetName(this OutboundDataRestriction restriction) => restriction switch
    {
        OutboundDataRestriction.ONLY_CONFIGURED_SERVICES => TB("Only services configured in AI Studio"),
        OutboundDataRestriction.ONLY_LINKS_FROM_CHAT => TB("Configured services and addresses from the chat"),
        OutboundDataRestriction.UNRESTRICTED => TB("No restriction"),

        _ => TB("Unknown restriction"),
    };

    /// <summary>
    /// What a chat may still do on this level, once it has read from the mailbox.
    /// </summary>
    public static string GetDescription(this OutboundDataRestriction restriction) => restriction switch
    {
        OutboundDataRestriction.ONLY_CONFIGURED_SERVICES => TB("The chat may only use services configured in AI Studio, such as this mailbox or your Confluence. It reads no web pages, and it does not search the web."),
        OutboundDataRestriction.ONLY_LINKS_FROM_CHAT => TB("The chat may also read web pages whose addresses stand in the chat, written by you or returned by a tool, exactly as they stand there. It does not search the web, and the AI cannot choose addresses of its own."),
        OutboundDataRestriction.UNRESTRICTED => TB("The chat may use every tool you chose, web search and any web page included. Content of your mails can then reach third parties, e.g., inside a search query or the address of a web page."),

        _ => TB("This version of AI Studio does not know this restriction, so it applies the strictest one."),
    };
}