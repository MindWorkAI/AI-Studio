namespace AIStudio.Tools.Mail;

/// <summary>
/// The header an address of a mail comes from.
/// </summary>
public enum MailAddressRole
{
    /// <summary>
    /// A role this version does not know, e.g. stored by a newer version.
    /// </summary>
    UNKNOWN,

    FROM,

    /// <summary>
    /// Who actually sent the mail when that is somebody else than the author, e.g. an assistant or a mailing list.
    /// </summary>
    SENDER,

    REPLY_TO,
    TO,
    CC,

    /// <summary>
    /// Only known for mails the user sent, whose copy keeps the blind copies.
    /// </summary>
    BCC,
}