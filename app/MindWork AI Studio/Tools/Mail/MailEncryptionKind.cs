namespace AIStudio.Tools.Mail;

/// <summary>
/// How the content of a mail is protected so that AI Studio cannot read it.
/// </summary>
/// <remarks>
/// Of such a mail, only its header block is indexed. A mail which is merely signed in the clear
/// (multipart/signed) stays readable and is NONE.
/// </remarks>
public enum MailEncryptionKind
{
    /// <summary>
    /// The content is readable.
    /// </summary>
    NONE,

    /// <summary>
    /// A kind this version does not know, e.g. stored by a newer version. Such a mail still counts as encrypted.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// S/MIME, enveloped data.
    /// </summary>
    SMIME,

    /// <summary>
    /// S/MIME with the signature wrapped around the content, which then cannot be read without the S/MIME envelope.
    /// </summary>
    SMIME_OPAQUE_SIGNED,

    /// <summary>
    /// PGP/MIME, multipart/encrypted.
    /// </summary>
    PGP_MIME,

    /// <summary>
    /// A PGP message block within the text of the mail.
    /// </summary>
    PGP_INLINE,

    /// <summary>
    /// Microsoft Information Rights Management, i.e., a message.rpmsg attachment.
    /// </summary>
    MICROSOFT_IRM,
}