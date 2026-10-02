using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Validation;

using MailKit;

using MimeKit;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Decides which attachments of a mail count as such, and which of them get their text read.
/// </summary>
/// <remarks>
/// Everything here works on the structure of a mail, so nothing of an attachment is fetched before
/// it is clear that its text is wanted. Read are documents alone, the same types a local folder
/// indexes. Whatever else a mail carries, e.g. images or archives, is known by its name only.
/// </remarks>
public static class MailAttachmentRules
{
    private const string BASE64_ENCODING = "base64";

    private const long BYTES_PER_MEGABYTE = 1024L * 1024L;

    /// <summary>
    /// Whether a part is the signature of a signed mail rather than something the sender attached.
    /// </summary>
    /// <remarks>
    /// A signed mail carries its signature as a part of its own, which many programs mark as an
    /// attachment. A reader never sees it as one, since a mail program shows a seal instead, and it
    /// has no text to read.
    /// </remarks>
    /// <param name="contentType">The content type of the part.</param>
    public static bool IsSignature(ContentType contentType) =>
        contentType.IsMimeType("application", "pkcs7-signature")
        || contentType.IsMimeType("application", "x-pkcs7-signature")
        || contentType.IsMimeType("application", "pgp-signature");

    /// <summary>
    /// Up to which size the text of an attachment is read.
    /// </summary>
    /// <remarks>
    /// A size beyond what the dialog allows, e.g. from a settings file written by hand, is brought
    /// back into that range. Attachments are a cost of their own, and no value should open them up
    /// without bounds.
    /// </remarks>
    /// <param name="mailbox">The mailbox.</param>
    /// <returns>The size in megabytes, or null when the mailbox reads no attachments at all.</returns>
    public static int? GetMaxSizeMegabytes(DataSourceMailbox mailbox) => mailbox.IndexAttachments
        ? Math.Clamp(mailbox.MaxAttachmentSizeMegabytes, DataSourceValidation.MIN_ATTACHMENT_SIZE_MEGABYTES, DataSourceValidation.MAX_ATTACHMENT_SIZE_MEGABYTES)
        : null;

    /// <summary>
    /// The file type of an attachment, from its name or else from its content type.
    /// </summary>
    /// <remarks>
    /// Only ever used after checking it against the known document types, which is what keeps a
    /// name of the sender's choosing from becoming part of a path.
    /// </remarks>
    /// <param name="part">The attachment.</param>
    /// <returns>The extension in lower case and without a dot, or empty when there is none.</returns>
    public static string GetExtension(BodyPartBasic part)
    {
        var extension = Path.GetExtension(MailTextNormalization.NormalizeHeaderValue(part.FileName)).TrimStart('.');
        if (extension.Length is 0 && MimeTypes.TryGetExtension(part.ContentType.MimeType, out var contentTypeExtension))
            extension = contentTypeExtension.TrimStart('.');

        return extension.ToLowerInvariant();
    }

    /// <summary>
    /// How large an attachment is once it is decoded, as far as the structure of the mail tells.
    /// </summary>
    /// <remarks>
    /// The server reports the size as the attachment travels, which with Base64 is a third more than
    /// the file the user sees. The estimate errs a little to the large side, since it counts the line
    /// breaks of the encoding as well.
    /// </remarks>
    /// <param name="part">The attachment.</param>
    /// <returns>The estimated size in bytes.</returns>
    public static long EstimateDecodedSize(BodyPartBasic part) => string.Equals(part.ContentTransferEncoding, BASE64_ENCODING, StringComparison.OrdinalIgnoreCase)
        ? part.Octets / 4L * 3L
        : part.Octets;

    /// <summary>
    /// Why the text of an attachment is not read.
    /// </summary>
    /// <param name="part">The attachment.</param>
    /// <param name="maxSizeMegabytes">Up to which size attachments are read, or null when none are, cf. GetMaxSizeMegabytes.</param>
    /// <returns>The reason, or null when the text is to be read.</returns>
    public static MailPartTextState? GetReasonToSkip(BodyPartBasic part, int? maxSizeMegabytes)
    {
        if (maxSizeMegabytes is not { } maxSize)
            return MailPartTextState.ATTACHMENTS_DISABLED;

        if (!FileTypes.IsAllowedExtension(GetExtension(part), FileTypes.DOCUMENT))
            return MailPartTextState.UNSUPPORTED_TYPE;

        if (EstimateDecodedSize(part) > maxSize * BYTES_PER_MEGABYTE)
            return MailPartTextState.TOO_LARGE;

        return null;
    }
}