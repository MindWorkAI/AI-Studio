namespace AIStudio.Tools.Mail;

/// <summary>
/// What a folder is for, as the server announces it (RFC 6154 and RFC 8457).
/// </summary>
public enum MailFolderSpecialUse
{
    /// <summary>
    /// An ordinary folder.
    /// </summary>
    NONE,

    /// <summary>
    /// A special use this version does not know, e.g. stored by a newer version.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// A virtual folder holding every mail of the mailbox, e.g. "All Mail" at Gmail.
    /// </summary>
    ALL,

    ARCHIVE,
    DRAFTS,

    /// <summary>
    /// A virtual folder holding every flagged mail.
    /// </summary>
    FLAGGED,

    IMPORTANT,
    JUNK,
    SENT,
    TRASH,
}