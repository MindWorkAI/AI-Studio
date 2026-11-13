using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// A count of the mails in the mailboxes, as the model asked for it and as far as it was allowed.
/// </summary>
/// <param name="Mailboxes">The mailboxes to count, in the order they are offered.</param>
/// <param name="Conditions">The conditions the mails have to meet.</param>
/// <param name="Grouping">How to break the number of each mailbox down.</param>
internal sealed record CountMailsRequest(IReadOnlyList<DataSourceMailbox> Mailboxes, MailConditions Conditions, MailCountGrouping Grouping);