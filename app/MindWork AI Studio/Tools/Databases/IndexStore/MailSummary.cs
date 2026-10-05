using AIStudio.Tools.Mail;

namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// What a list of mails shows about one of them, without its text.
/// </summary>
/// <param name="MailId">The id of the mail.</param>
/// <param name="Subject">The subject of the mail.</param>
/// <param name="ReceivedAtUtc">When the mail arrived at the server.</param>
/// <param name="SentAtUtc">When the sender says the mail was written, or null when it does not say so readably.</param>
/// <param name="MessageId">The Message-ID header, empty when the mail has none.</param>
/// <param name="InReplyTo">The Message-ID the In-Reply-To header names, empty when there is none.</param>
/// <param name="Addresses">The addresses from the header, grouped by their role.</param>
/// <param name="FolderPaths">The folders the mail lies in, ordered by path.</param>
/// <param name="Flags">The flags of the mail; each one is set when it is set in any folder.</param>
/// <param name="Importance">How important the sender marked the mail.</param>
/// <param name="EncryptionKind">How the content of the mail is encrypted, NONE when it is not.</param>
/// <param name="AttachmentNames">The file names of the attachments, in their order.</param>
public sealed record MailSummary(
    string MailId,
    string Subject,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? SentAtUtc,
    string MessageId,
    string InReplyTo,
    IReadOnlyList<MailAddressRecord> Addresses,
    IReadOnlyList<string> FolderPaths,
    MailFlags Flags,
    MailImportance Importance,
    MailEncryptionKind EncryptionKind,
    IReadOnlyList<string> AttachmentNames);