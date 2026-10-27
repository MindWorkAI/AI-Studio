using MimeKit;

namespace AIStudio.Tools.Mail;

/// <summary>
/// What MailTextBuilder reads one mail from.
/// </summary>
/// <remarks>
/// Not a whole mail on purpose: the sync fetches the header block and the structure first, and
/// the text parts only of a mail which turns out readable. The attachments themselves are never
/// part of this, only their names.
/// </remarks>
/// <param name="Headers">The complete header block of the mail.</param>
/// <param name="HtmlBody">The HTML part a reader sees, decoded from its transfer encoding and charset, or null when there is none.</param>
/// <param name="TextBody">The plain text part, decoded likewise, or null when there is none.</param>
/// <param name="AttachmentNames">The file names of the attachments, in their order, without the signature of a signed mail.</param>
/// <param name="StructureEncryption">How the structure of the mail says its content is encrypted, see MailEncryptionDetection.</param>
public sealed record MailTextSource(HeaderList Headers, string? HtmlBody, string? TextBody, IReadOnlyList<string> AttachmentNames, MailEncryptionKind StructureEncryption)
{
    /// <summary>
    /// Takes everything from a mail which was fetched or loaded as a whole.
    /// </summary>
    /// <param name="message">The parsed mail.</param>
    /// <returns>The source for MailTextBuilder.</returns>
    public static MailTextSource FromMessage(MimeMessage message)
    {
        var attachmentNames = message.Attachments
            .Where(attachment => !MailAttachmentRules.IsSignature(attachment.ContentType))
            .Select(attachment => attachment.ContentDisposition?.FileName ?? attachment.ContentType.Name ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return new(message.Headers, message.HtmlBody, message.TextBody, attachmentNames, MailEncryptionDetection.Detect(message.Body));
    }
}