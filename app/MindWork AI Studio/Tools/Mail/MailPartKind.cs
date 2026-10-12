namespace AIStudio.Tools.Mail;

/// <summary>
/// Which part of a mail some stored text belongs to.
/// </summary>
public enum MailPartKind
{
    /// <summary>
    /// A kind this version does not know, e.g. stored by a newer version.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// The complete header block, as the server delivered it.
    /// </summary>
    HEADERS,

    /// <summary>
    /// The text of the mail.
    /// </summary>
    BODY,

    ATTACHMENT,
}