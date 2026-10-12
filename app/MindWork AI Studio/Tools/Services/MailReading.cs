using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;

namespace AIStudio.Tools.Services;

/// <summary>
/// Everything the index keeps about one mail, together with the mailbox it belongs to.
/// </summary>
/// <param name="Mailbox">The mailbox, which the provider of the chat may read.</param>
/// <param name="Summary">What a list of mails shows about the mail, e.g., its subject and its folders.</param>
/// <param name="Mail">Its addresses, its parts as AI Studio read them, and its places on the server.</param>
/// <param name="InReplyToMailId">The id of the mail it replies to, or null when the index does not hold that one.</param>
/// <param name="MailboxFolders">The folders of the mailbox, which tell what the folders of the mail are for, e.g., the sent mails.</param>
public sealed record MailReading(DataSourceMailbox Mailbox, MailSummary Summary, MailRecord Mail, string? InReplyToMailId, IReadOnlyList<MailFolderRecord> MailboxFolders);