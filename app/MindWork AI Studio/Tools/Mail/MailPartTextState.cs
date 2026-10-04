namespace AIStudio.Tools.Mail;

/// <summary>
/// Whether the text of a part of a mail could be read and, when not, why.
/// </summary>
public enum MailPartTextState
{
    /// <summary>
    /// A state this version does not know, e.g. stored by a newer version.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// The text was read and is stored.
    /// </summary>
    EXTRACTED,

    /// <summary>
    /// The mailbox is set to leave attachments out.
    /// </summary>
    ATTACHMENTS_DISABLED,

    /// <summary>
    /// The attachment is larger than the mailbox allows to read.
    /// </summary>
    TOO_LARGE,

    /// <summary>
    /// The attachment is of a type AI Studio cannot read text from, e.g. an image.
    /// </summary>
    UNSUPPORTED_TYPE,

    /// <summary>
    /// Reading the text was tried but failed.
    /// </summary>
    EXTRACTION_FAILED,
}