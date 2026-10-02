using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using AIStudio.Tools.Databases.IndexStore;

using MailKit;

using MimeKit;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Turns what the server reported about a mail into what AI Studio keeps of it.
/// </summary>
/// <remarks>
/// Everything is read from the header block the sync fetches with every mail, not from the
/// ENVELOPE of IMAP: a server fills in Sender and Reply-To from From when the mail has neither, and
/// the trust check of a later step has to know whether the sender set them.
/// </remarks>
public static class MailSummaryReader
{
    /// <summary>
    /// Reads what tells the mail apart from all others on the server, for MailContentKey.
    /// </summary>
    /// <param name="summary">The summary, fetched with its header block.</param>
    /// <returns>The identity of the mail.</returns>
    public static MailIdentity ReadIdentity(IMessageSummary summary)
    {
        var headers = GetHeaders(summary);
        return new(
            summary.EmailId,
            summary.GMailMessageId,
            headers[HeaderId.MessageId],
            MailHeaders.ReadDate(headers),
            MailHeaders.ReadMailboxes(headers, HeaderId.From).Select(mailbox => mailbox.Address).ToList(),
            headers[HeaderId.Subject],
            summary.Size ?? 0);
    }

    /// <summary>
    /// Puts together what MailTextBuilder reads a mail from.
    /// </summary>
    /// <param name="summary">The summary, fetched with its header block and its structure.</param>
    /// <param name="textParts">The text parts of the mail, or null when they were not fetched because the mail is encrypted.</param>
    /// <returns>The source for MailTextBuilder.</returns>
    public static MailTextSource ReadTextSource(IMessageSummary summary, MailTextParts? textParts)
    {
        var attachmentNames = ReadAttachments(summary)
            .Select(attachment => attachment.FileName ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return new(GetHeaders(summary), textParts?.HtmlBody, textParts?.TextBody, attachmentNames, MailEncryptionDetection.Detect(summary.Body));
    }

    /// <summary>
    /// Reads which attachments a reader of the mail sees, from its structure.
    /// </summary>
    /// <param name="summary">The summary, fetched with its structure.</param>
    /// <returns>The attachments in their order, without the signature of a signed mail, cf. MailAttachmentRules.IsSignature.</returns>
    public static IReadOnlyList<BodyPartBasic> ReadAttachments(IMessageSummary summary) => summary.Attachments
        .Where(attachment => !MailAttachmentRules.IsSignature(attachment.ContentType))
        .ToList();

    /// <summary>
    /// Reads every address of the header block together with the header it comes from.
    /// </summary>
    /// <param name="headers">The header block.</param>
    /// <returns>The addresses, grouped by their header in the order From, Sender, Reply-To, To, Cc, Bcc.</returns>
    public static IReadOnlyList<MailAddressRecord> ReadAddresses(HeaderList headers)
    {
        (HeaderId HeaderId, MailAddressRole Role)[] roles =
        [
            (HeaderId.From, MailAddressRole.FROM),
            (HeaderId.Sender, MailAddressRole.SENDER),
            (HeaderId.ReplyTo, MailAddressRole.REPLY_TO),
            (HeaderId.To, MailAddressRole.TO),
            (HeaderId.Cc, MailAddressRole.CC),
            (HeaderId.Bcc, MailAddressRole.BCC),
        ];

        return roles
            .SelectMany(role => MailHeaders.ReadMailboxes(headers, role.HeaderId).Select(mailbox => new MailAddressRecord(
                role.Role,
                MailTextNormalization.NormalizeHeaderValue(mailbox.Address),
                MailTextNormalization.NormalizeHeaderValue(mailbox.Name))))
            .Where(address => address.Address.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Reads the flags AI Studio keeps of a mail.
    /// </summary>
    /// <param name="flags">The flags the server reported, or null when it reported none.</param>
    /// <returns>The flags.</returns>
    public static MailFlags ReadFlags(MessageFlags? flags)
    {
        var value = flags ?? MessageFlags.None;
        return new(value.HasFlag(MessageFlags.Seen), value.HasFlag(MessageFlags.Flagged), value.HasFlag(MessageFlags.Answered));
    }

    /// <summary>
    /// Writes the header block out the way the server delivered it, encoded words and all.
    /// </summary>
    /// <remarks>
    /// This is what the trust check reads, and it needs the header block as it is: the decoded
    /// text of a header hides whether it was encoded, and how.
    /// </remarks>
    /// <param name="headers">The header block.</param>
    /// <returns>The header block as text.</returns>
    public static string ReadHeaderBlock(HeaderList headers)
    {
        using var stream = new MemoryStream();
        headers.WriteTo(FormatOptions.Default, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Hashes the mail as the server delivered it, independent of how AI Studio turns it into text.
    /// </summary>
    /// <remarks>
    /// A mail on an IMAP server never changes, so its header block, its structure and its size are
    /// enough to tell it from any other mail, without fetching the whole of it.
    /// </remarks>
    /// <param name="summary">The summary, fetched with its header block and its structure.</param>
    /// <returns>The hash in hex.</returns>
    public static string ComputeMailHash(IMessageSummary summary)
    {
        var source = string.Join('\n',
            ReadHeaderBlock(GetHeaders(summary)),
            summary.Body?.ToString() ?? string.Empty,
            (summary.Size ?? 0).ToString(CultureInfo.InvariantCulture));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static HeaderList GetHeaders(IMessageSummary summary) => summary.Headers ?? throw new InvalidOperationException("The mail was fetched without its header block.");
}