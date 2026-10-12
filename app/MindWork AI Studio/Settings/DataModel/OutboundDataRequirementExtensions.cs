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
    /// same in both places, and the model can pass it on. It names the setting as the mailbox
    /// dialog labels it, so the user finds it there. The level is the one the setting had when the
    /// chat read the mails: a change made later does not loosen the chat.
    /// </remarks>
    /// <param name="requirement">What the chat demands.</param>
    /// <param name="mailboxes">The configured mailboxes, to find the name of the one which demands it.</param>
    /// <returns>The text for the user.</returns>
    public static string GetToolBlockedMessage(this OutboundDataRequirement requirement, IEnumerable<DataSourceMailbox> mailboxes)
    {
        var mailboxName = mailboxes
            .Where(mailbox => string.Equals(mailbox.Id, requirement.DataSourceId, StringComparison.Ordinal))
            .Select(mailbox => mailbox.Name)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(mailboxName)
            ? string.Format(TB("This chat read mails from a mailbox which has been removed since, while its setting 'Where a chat may send data after reading mails' was '{0}'. This tool would send data beyond that, so it is not available in this chat. A new chat can use it again."), requirement.Restriction.GetName())
            : string.Format(TB("This chat read mails from the mailbox '{0}' while its setting 'Where a chat may send data after reading mails' was '{1}'. This tool would send data beyond that, so it is not available in this chat. A new chat can use it again."), mailboxName, requirement.Restriction.GetName());
    }
}