using MailKit;
using MimeKit;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Finds out whether AI Studio can read the content of a mail.
/// </summary>
/// <remarks>
/// The structure of a mail tells most of it, so the sync asks before it fetches anything: from the
/// BODYSTRUCTURE the server describes, the encrypted blob never has to be downloaded. A parsed mail
/// gives the same answer through its MIME tree. Only a PGP block within the text needs the text
/// itself, see ContainsInlinePgpMessage.
///
/// An encrypted part counts wherever it sits, apart from mails attached to this one: some gateways
/// wrap an encrypted mail together with a disclaimer of their own, and that disclaimer is not what
/// the mail says. An attached mail keeps its protection to itself, though, because the mail around
/// it is readable.
///
/// A mail which is merely signed in the clear (multipart/signed) stays readable: its first part is
/// the content as it is, and the signature next to it is no encryption.
/// </remarks>
public static class MailEncryptionDetection
{
    /// <summary>
    /// The line which opens an encrypted PGP block, from RFC 4880. A signed block opens with
    /// "-----BEGIN PGP SIGNED MESSAGE-----" instead and stays readable.
    /// </summary>
    private const string PGP_MESSAGE_ARMOR = "-----BEGIN PGP MESSAGE-----";

    /// <summary>
    /// The name Outlook gives the attachment which holds a mail protected by Information Rights Management.
    /// </summary>
    private const string IRM_FILE_NAME = "message.rpmsg";

    /// <summary>
    /// Reads the encryption from the structure the server describes.
    /// </summary>
    /// <remarks>
    /// The structure is walked with a stack of its own rather than by recursion: the IMAP client reads
    /// a structure of any depth, and a mail nested deeply enough on purpose would otherwise end AI
    /// Studio with a stack overflow, which no catch block stops. The parts are visited in their order,
    /// so the first encrypted part decides, as for a parsed mail.
    /// </remarks>
    /// <param name="bodyStructure">The BODYSTRUCTURE of the mail, or null when the server sent none.</param>
    /// <returns>How the content is encrypted, NONE when it is readable as far as the structure tells.</returns>
    public static MailEncryptionKind Detect(BodyPart? bodyStructure)
    {
        var pending = new Stack<BodyPart>();
        if (bodyStructure is not null)
            pending.Push(bodyStructure);

        while (pending.TryPop(out var current))
        {
            switch (current)
            {
                // An attached mail keeps its protection to itself:
                case BodyPartMessage:
                    continue;

                case BodyPartMultipart multipart when IsPgpMime(multipart.ContentType):
                    return MailEncryptionKind.PGP_MIME;

                case BodyPartMultipart multipart:
                    // Pushed in reverse, so that the first part comes off the stack first:
                    for (var index = multipart.BodyParts.Count - 1; index >= 0; index--)
                        pending.Push(multipart.BodyParts[index]);

                    continue;

                case BodyPartBasic part:
                    var kind = DetectPart(part.ContentType, part.FileName);
                    if (kind is not MailEncryptionKind.NONE)
                        return kind;

                    continue;
            }
        }

        return MailEncryptionKind.NONE;
    }

    /// <summary>
    /// Reads the encryption from the MIME tree of a parsed mail.
    /// </summary>
    /// <param name="body">The body of the mail, or null when only its header block was loaded.</param>
    /// <returns>How the content is encrypted, NONE when it is readable as far as the structure tells.</returns>
    public static MailEncryptionKind Detect(MimeEntity? body) => body switch
    {
        // An attached mail keeps its protection to itself:
        MessagePart => MailEncryptionKind.NONE,

        Multipart multipart => DetectMultipart(multipart.ContentType, multipart.Select(Detect)),
        MimePart part => DetectPart(part.ContentType, part.FileName),
        _ => MailEncryptionKind.NONE,
    };

    /// <summary>
    /// Whether the text of a mail holds an encrypted PGP block, which the structure cannot show:
    /// such a mail is plain text like any other.
    /// </summary>
    /// <param name="text">The text of the mail as it was sent, HTML or plain.</param>
    /// <returns>True when the text holds such a block.</returns>
    public static bool ContainsInlinePgpMessage(string? text) => text?.Contains(PGP_MESSAGE_ARMOR, StringComparison.Ordinal) is true;

    private static MailEncryptionKind DetectMultipart(ContentType contentType, IEnumerable<MailEncryptionKind> partKinds)
    {
        if (IsPgpMime(contentType))
            return MailEncryptionKind.PGP_MIME;

        return partKinds.FirstOrDefault(kind => kind is not MailEncryptionKind.NONE);
    }

    /// <summary>
    /// Whether a multipart is encrypted. RFC 1847 knows no other use of multipart/encrypted than PGP/MIME in practice.
    /// </summary>
    private static bool IsPgpMime(ContentType contentType) => contentType.IsMimeType("multipart", "encrypted");

    private static MailEncryptionKind DetectPart(ContentType contentType, string? fileName)
    {
        if (contentType.IsMimeType("application", "pkcs7-mime") || contentType.IsMimeType("application", "x-pkcs7-mime"))
        {
            //
            // Signed data wraps the content into the signature, so it cannot be read without
            // unpacking the envelope. Everything else is encrypted, and so is a part without any
            // smime-type: Outlook sends enveloped data like that, too.
            //
            return contentType.Parameters.TryGetValue("smime-type", out string? smimeType) && "signed-data".Equals(smimeType, StringComparison.OrdinalIgnoreCase)
                ? MailEncryptionKind.SMIME_OPAQUE_SIGNED
                : MailEncryptionKind.SMIME;
        }

        // Some programs send the S/MIME envelope as plain bytes, recognizable by its name only:
        if (fileName?.EndsWith(".p7m", StringComparison.OrdinalIgnoreCase) is true)
            return MailEncryptionKind.SMIME;

        if (contentType.IsMimeType("application", "x-microsoft-rpmsg-message") || IRM_FILE_NAME.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            return MailEncryptionKind.MICROSOFT_IRM;

        return MailEncryptionKind.NONE;
    }
}