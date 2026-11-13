using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class OutboundDataRequirementExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(OutboundDataRequirementExtensions).Namespace, nameof(OutboundDataRequirementExtensions));

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(OutboundDataRequirementExtensions).Namespace, nameof(OutboundDataRequirementExtensions));
    /// <param name="requirement">What the chat demands.</param>
    extension(OutboundDataRequirement requirement)
    {
        /// <summary>
        /// Why a tool cannot run in a chat with this requirement, and how to get it back.
        /// </summary>
        /// <remarks>
        /// One text for the tool selection and for a call which was turned down, so the user reads the
        /// same in both places, and the model can pass it on. It names the source which demands the
        /// requirement, and the setting as the user finds it labeled. The level is the one the setting
        /// had when the content was read: a change made later does not loosen the chat.<br/><br/>
        /// The text speaks of content the chat holds, not of content it read: the content may come from
        /// another chat or an assistant, which sent it here along with its restrictions.
        /// </remarks>
        /// <param name="settings">The configuration, to find the name of the mailbox or data source which demands it.</param>
        /// <returns>The text for the user.</returns>
        public string GetToolBlockedMessage(Data settings) => requirement.Origin switch
        {
            OutboundDataOrigin.MAILBOX => GetMailboxBlockedMessage(requirement, settings.Mailboxes),
            OutboundDataOrigin.CONFLUENCE_WIKI => string.Format(TA("This chat holds content from your Confluence wiki, found while the setting 'Where a chat may send data after reading internal pages' of the tool 'Read Web Page' was '{0}'. This tool would send data beyond that, so it is not available in this chat. A new chat without this content can use it again."), requirement.Restriction.Name),
            OutboundDataOrigin.INTERNAL_WEB_PAGE => string.Format(TB("This chat holds content from an internal web page on '{0}', read while the setting 'Where a chat may send data after reading internal pages' of the tool 'Read Web Page' was '{1}'. This tool would send data beyond that, so it is not available in this chat. A new chat without this content can use it again."), requirement.Reference, requirement.Restriction.Name),
            OutboundDataOrigin.DATA_SOURCE => GetDataSourceBlockedMessage(requirement, settings.DataSources),
            OutboundDataOrigin.BEYOND_FACTS_LAB => TB("This chat holds a preparation of the BeyondFacts Lab, which may be unpublished, so it reads only web pages whose addresses stand in the chat. This tool would send data beyond that, so it is not available in this chat. The lab searches the web in a research of its own, which receives only the public details about the event. A new chat without this content can use it again."),

            // NONE keeps no tool from running, since nothing restricts such a chat. What is left is an
            // origin this version does not know:
            _ => string.Format(TB("This chat holds internal content, read while a chat was restricted to '{0}'. This tool would send data beyond that, so it is not available in this chat. A new chat without this content can use it again."), requirement.Restriction.Name),
        };
    }

    private static string GetMailboxBlockedMessage(OutboundDataRequirement requirement, IEnumerable<DataSourceMailbox> mailboxes)
    {
        var mailboxName = mailboxes
            .Where(mailbox => string.Equals(mailbox.Id, requirement.Reference, StringComparison.Ordinal))
            .Select(mailbox => mailbox.Name)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(mailboxName)
            ? string.Format(TB("This chat holds content from a mailbox which has been removed since, read while its setting 'Where a chat may send data after reading mails' was '{0}'. This tool would send data beyond that, so it is not available in this chat. A new chat without this content can use it again."), requirement.Restriction.Name)
            : string.Format(TB("This chat holds content from the mailbox '{0}', read while its setting 'Where a chat may send data after reading mails' was '{1}'. This tool would send data beyond that, so it is not available in this chat. A new chat without this content can use it again."), mailboxName, requirement.Restriction.Name);
    }

    private static string GetDataSourceBlockedMessage(OutboundDataRequirement requirement, IEnumerable<IDataSource> dataSources)
    {
        var dataSourceName = dataSources
            .Where(dataSource => string.Equals(dataSource.Id, requirement.Reference, StringComparison.Ordinal))
            .Select(dataSource => dataSource.Name)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(dataSourceName)
            ? string.Format(TB("This chat holds content from a data source which has been removed since, found while its setting 'Where a chat may send data after reading this data source' was '{0}'. This tool would send data beyond that, so it is not available in this chat. A new chat without this content can use it again."), requirement.Restriction.Name)
            : string.Format(TB("This chat holds content from the data source '{0}', found while its setting 'Where a chat may send data after reading this data source' was '{1}'. This tool would send data beyond that, so it is not available in this chat. A new chat without this content can use it again."), dataSourceName, requirement.Restriction.Name);
    }
}