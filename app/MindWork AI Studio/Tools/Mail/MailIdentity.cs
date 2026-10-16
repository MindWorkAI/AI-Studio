namespace AIStudio.Tools.Mail;

/// <summary>
/// What tells one mail apart from all others on a server, as MailContentKey reads it.
/// </summary>
/// <param name="EmailId">The EMAILID of RFC 8474, or null when the server does not know OBJECTID.</param>
/// <param name="GmailMessageId">The X-GM-MSGID, or null when the server is not Gmail.</param>
/// <param name="MessageId">The Message-ID header, or null when the mail has none.</param>
/// <param name="Date">The Date header, or null when the mail has none or it cannot be read.</param>
/// <param name="FromAddresses">The addresses of the From header, in their order.</param>
/// <param name="Subject">The subject, or null when the mail has none.</param>
/// <param name="Size">The size of the mail on the server (RFC822.SIZE), in bytes.</param>
public sealed record MailIdentity(string? EmailId, ulong? GmailMessageId, string? MessageId, DateTimeOffset? Date, IReadOnlyList<string> FromAddresses, string? Subject, long Size);