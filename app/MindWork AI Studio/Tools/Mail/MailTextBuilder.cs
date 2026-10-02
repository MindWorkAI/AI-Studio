using System.Globalization;
using System.Text;

using MimeKit;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Turns a mail into the text AI Studio indexes: a header block, then the text a reader of the mail sees.
/// </summary>
/// <remarks>
/// The text comes from the HTML part whenever there is one. It is what the reader sees, while
/// the plain text part is only what the sender's program claims the mail says, and the two may
/// say different things. The plain text part is read when there is no HTML part, or when nothing
/// visible remains of it. See MailHtmlConverter for what the HTML loses on the way.
///
/// An encrypted mail keeps its header block alone, together with a line saying that and how it is
/// encrypted. Nothing of the encrypted content gets in, neither its blob nor the names of its
/// attachments, and a model can tell the user why it does not know what the mail says.
///
/// The header block is written in English whatever the language of the app, since it is data for
/// the index and the model, like the field names of a tool result. All of it comes from the
/// sender and goes through the prompt injection filter just as the body does.
///
/// The folder is left out of the header block on purpose. A mail which moves to another folder is
/// not embedded again, so a folder written into its text would soon name the wrong one. Where a
/// mail lies is known from its locations in the index instead.
/// </remarks>
public static class MailTextBuilder
{
    /// <summary>
    /// How many addresses of one header are named before the rest is only counted. A mail to a
    /// whole department would fill the first chunk with addresses otherwise.
    /// </summary>
    private const int MAX_LISTED_ADDRESSES = 10;

    /// <summary>
    /// How many attachments are named before the rest is only counted.
    /// </summary>
    private const int MAX_LISTED_ATTACHMENTS = 20;

    /// <summary>
    /// Builds the text of one mail.
    /// </summary>
    /// <param name="source">The parts of the mail.</param>
    /// <returns>The text of the mail.</returns>
    public static MailText Build(MailTextSource source)
    {
        var encryptionKind = source.StructureEncryption;
        if (encryptionKind is MailEncryptionKind.NONE && (MailEncryptionDetection.ContainsInlinePgpMessage(source.HtmlBody) || MailEncryptionDetection.ContainsInlinePgpMessage(source.TextBody)))
            encryptionKind = MailEncryptionKind.PGP_INLINE;

        var isReadable = encryptionKind is MailEncryptionKind.NONE;
        var importance = MailImportanceDetection.Detect(source.Headers);
        var subject = MailTextNormalization.NormalizeHeaderValue(source.Headers[HeaderId.Subject]);
        IReadOnlyList<string> attachmentNames = isReadable ? source.AttachmentNames : [];

        var headerBlock = BuildHeaderBlock(source.Headers, subject, importance, attachmentNames, encryptionKind);
        var (body, bodySource) = isReadable ? ReadBody(source) : (string.Empty, MailBodySource.NONE);
        return new(headerBlock, body, subject, encryptionKind, importance, bodySource);
    }

    private static (string Body, MailBodySource Source) ReadBody(MailTextSource source)
    {
        if (!string.IsNullOrWhiteSpace(source.HtmlBody))
        {
            var markdown = MailHtmlConverter.ToMarkdown(source.HtmlBody);
            if (markdown.Length > 0)
                return (markdown, MailBodySource.HTML);
        }

        if (string.IsNullOrWhiteSpace(source.TextBody))
            return (string.Empty, MailBodySource.NONE);

        var text = MailTextNormalization.NormalizeBody(source.TextBody).Trim();
        return text.Length > 0 ? (text, MailBodySource.PLAIN_TEXT) : (string.Empty, MailBodySource.NONE);
    }

    private static string BuildHeaderBlock(HeaderList headers, string subject, MailImportance importance, IReadOnlyList<string> attachmentNames, MailEncryptionKind encryptionKind)
    {
        var block = new StringBuilder();
        AppendLine(block, "From", FormatAddresses(headers, HeaderId.From));
        AppendLine(block, "To", FormatAddresses(headers, HeaderId.To));
        AppendLine(block, "Cc", FormatAddresses(headers, HeaderId.Cc));
        AppendLine(block, "Subject", subject);
        AppendLine(block, "Date", MailHeaders.ReadDate(headers)?.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture) ?? string.Empty);
        AppendLine(block, "Importance", importance switch
        {
            MailImportance.HIGH => "high",
            MailImportance.LOW => "low",

            // Normal is what nearly every mail is, so it is not worth a line:
            _ => string.Empty,
        });
        AppendLine(block, "Attachments", FormatList(attachmentNames.Select(MailTextNormalization.NormalizeHeaderValue).Where(name => name.Length > 0).ToList(), MAX_LISTED_ATTACHMENTS));
        AppendLine(block, "Content", DescribeEncryption(encryptionKind));
        return block.ToString().TrimEnd('\n');
    }

    private static void AppendLine(StringBuilder block, string label, string value)
    {
        if (value.Length > 0)
            block.Append(label).Append(": ").Append(value).Append('\n');
    }

    /// <summary>
    /// Names the addresses of all headers of one kind, e.g. both To lines of a mail which has two.
    /// </summary>
    private static string FormatAddresses(HeaderList headers, HeaderId headerId) =>
        FormatList(MailHeaders.ReadMailboxes(headers, headerId).Select(FormatAddress).Where(address => address.Length > 0).ToList(), MAX_LISTED_ADDRESSES);

    private static string FormatAddress(MailboxAddress mailbox)
    {
        var address = MailTextNormalization.NormalizeHeaderValue(mailbox.Address);
        var name = MailTextNormalization.NormalizeHeaderValue(mailbox.Name);
        if (name.Length is 0 || name.Equals(address, StringComparison.OrdinalIgnoreCase))
            return address;

        return address.Length is 0 ? name : $"{name} <{address}>";
    }

    private static string FormatList(IReadOnlyList<string> items, int maxListed)
    {
        if (items.Count <= maxListed)
            return string.Join(", ", items);

        return $"{string.Join(", ", items.Take(maxListed))}, and {(items.Count - maxListed).ToString(CultureInfo.InvariantCulture)} more";
    }

    private static string DescribeEncryption(MailEncryptionKind encryptionKind) => encryptionKind switch
    {
        MailEncryptionKind.NONE => string.Empty,
        MailEncryptionKind.SMIME => "encrypted with S/MIME, AI Studio cannot read it",
        MailEncryptionKind.SMIME_OPAQUE_SIGNED => "wrapped into an S/MIME signature, AI Studio cannot read it",
        MailEncryptionKind.PGP_MIME => "encrypted with PGP/MIME, AI Studio cannot read it",
        MailEncryptionKind.PGP_INLINE => "encrypted with PGP, AI Studio cannot read it",
        MailEncryptionKind.MICROSOFT_IRM => "protected by Microsoft Information Rights Management, AI Studio cannot read it",
        _ => "encrypted, AI Studio cannot read it",
    };
}