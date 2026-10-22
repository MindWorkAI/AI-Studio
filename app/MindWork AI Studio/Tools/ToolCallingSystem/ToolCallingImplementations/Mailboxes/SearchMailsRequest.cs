using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// A search of the mailboxes, as the model asked for it and as far as it was allowed.
/// </summary>
/// <param name="Query">What to search for, or null to list the mails meeting the conditions.</param>
/// <param name="Mailboxes">The mailboxes to search, in the order they are offered.</param>
/// <param name="Conditions">The conditions the mails have to meet.</param>
/// <param name="Page">The page of results, starting at 1.</param>
internal sealed record SearchMailsRequest(string? Query, IReadOnlyList<DataSourceMailbox> Mailboxes, MailConditions Conditions, int Page);