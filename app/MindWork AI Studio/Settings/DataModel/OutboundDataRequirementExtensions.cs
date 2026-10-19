using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class OutboundDataRequirementExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(OutboundDataRequirementExtensions).Namespace, nameof(OutboundDataRequirementExtensions));

    /// <summary>
    /// Why a tool cannot run in a chat with this requirement, and how to get it back.
    /// </summary>
    /// <remarks>
    /// One text for the tool selection and for a call which was turned down, so the user reads the
    /// same in both places, and the model can pass it on. It names the source which demands the
    /// requirement, and the setting as the user finds it labeled. The level is the one the setting
    /// had when the chat read the content: a change made later does not loosen the chat.
    /// </remarks>
    /// <param name="requirement">What the chat demands.</param>
    /// <param name="settings">The configuration, to find the name of the mailbox which demands it.</param>
    /// <returns>The text for the user.</returns>
    public static string GetToolBlockedMessage(this OutboundDataRequirement requirement, Data settings) => requirement.Origin switch
    {
        OutboundDataOrigin.MAILBOX => GetMailboxBlockedMessage(requirement, settings.Mailboxes),
        OutboundDataOrigin.CONFLUENCE_WIKI => string.Format(TB("This chat searched your Confluence wiki while the setting 'Where a chat may send data after reading internal pages' of the tool 'Read Web Page' was '{0}'. This tool would send data beyond that, so it is not available in this chat. A new chat can use it again."), requirement.Restriction.GetName()),
        OutboundDataOrigin.INTERNAL_WEB_PAGE => string.Format(TB("This chat read an internal web page on '{0}' while the setting 'Where a chat may send data after reading internal pages' of the tool 'Read Web Page' was '{1}'. This tool would send data beyond that, so it is not available in this chat. A new chat can use it again."), requirement.Reference, requirement.Restriction.GetName()),

        // NONE keeps no tool from running, since nothing restricts such a chat. What is left is an
        // origin this version does not know:
        _ => string.Format(TB("This chat read internal content while the chat was restricted to '{0}'. This tool would send data beyond that, so it is not available in this chat. A new chat can use it again."), requirement.Restriction.GetName()),
    };

    private static string GetMailboxBlockedMessage(OutboundDataRequirement requirement, IEnumerable<DataSourceMailbox> mailboxes)
    {
        var mailboxName = mailboxes
            .Where(mailbox => string.Equals(mailbox.Id, requirement.Reference, StringComparison.Ordinal))
            .Select(mailbox => mailbox.Name)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(mailboxName)
            ? string.Format(TB("This chat read mails from a mailbox which has been removed since, while its setting 'Where a chat may send data after reading mails' was '{0}'. This tool would send data beyond that, so it is not available in this chat. A new chat can use it again."), requirement.Restriction.GetName())
            : string.Format(TB("This chat read mails from the mailbox '{0}' while its setting 'Where a chat may send data after reading mails' was '{1}'. This tool would send data beyond that, so it is not available in this chat. A new chat can use it again."), mailboxName, requirement.Restriction.GetName());
    }
}