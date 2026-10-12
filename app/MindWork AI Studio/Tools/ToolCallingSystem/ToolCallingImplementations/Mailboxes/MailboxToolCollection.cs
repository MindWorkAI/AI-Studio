using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// The tools which read the mailboxes of the user: Search Mails, Read Mail, and Count Mails.
/// </summary>
/// <remarks>
/// All three see the same mails, so they need the same trust, and searching without reading or
/// counting would only get in the way. Each mailbox states the confidence it needs, and only a
/// provider which meets it may read the mailbox, see MailboxRetrievalService.GetReadableMailboxes.
/// A provider below the lowest level a mailbox may ask for cannot read any mailbox, which is why
/// the collection asks for that level.
/// </remarks>
public sealed class MailboxToolCollection : IToolCollection
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MailboxToolCollection).Namespace, nameof(MailboxToolCollection));

    public ToolCollectionDefinition GetDefinition() => new()
    {
        Id = ToolSelectionRules.MAILBOXES_COLLECTION_ID,
        ToolIds = [ToolSelectionRules.SEARCH_MAILS_TOOL_ID, ToolSelectionRules.READ_MAIL_TOOL_ID, ToolSelectionRules.COUNT_MAILS_TOOL_ID],
        MinimumProviderConfidence = ConfidenceLevel.VERY_LOW,
        DescriptionForLLM = "Search, read, and count the mails in the mailboxes of the user, including their attachments. AI Studio keeps the mailboxes in a local index; nothing in a mailbox changes.",
    };

    public string Icon => Icons.Material.Filled.Mail;

    public string GetDisplayName() => TB("Mailboxes");

    public string GetDescription() => TB("Lets the AI search, read, and count the mails in your mailboxes, including their attachments.");
}